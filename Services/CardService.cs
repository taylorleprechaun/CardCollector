using CardCollector.Data.Models;
using CardCollector.DTO;
using CardCollector.Extensions;
using CardCollector.Repository;
using CardCollector.ViewModels;
using Microsoft.Extensions.Configuration;

namespace CardCollector.Services
{
    public sealed class CardService : ICardService
    {
        private readonly ICardDataRepository _cardDataRepository;
        private readonly ICollectionEntryValueRepository _collectionEntryValueRepository;
        private readonly ICollectionRepository _collectionRepository;
        private readonly ICollectionValueRepository _collectionValueRepository;
        private readonly IPreferredVersionRepository _preferredVersionRepository;
        private readonly int _pricingDelayMs;
        private readonly IPricingService _pricingService;

        public CardService(
            ICardDataRepository cardDataRepository,
            ICollectionRepository collectionRepository,
            ICollectionEntryValueRepository collectionEntryValueRepository,
            ICollectionValueRepository collectionValueRepository,
            IPreferredVersionRepository preferredVersionRepository,
            IPricingService pricingService,
            IConfiguration config)
        {
            _cardDataRepository = cardDataRepository;
            _collectionEntryValueRepository = collectionEntryValueRepository;
            _collectionRepository = collectionRepository;
            _collectionValueRepository = collectionValueRepository;
            _preferredVersionRepository = preferredVersionRepository;
            _pricingDelayMs = config.GetValue<int>("CardDataSettings:PricingDelayMs", 100);
            _pricingService = pricingService;
        }

        public async Task<bool> AddEntryAsync(
            int cardID, int imageID, string setCode, CollectionStatus status,
            int quantity, CardCondition? condition, CardEdition? edition,
            AcquisitionMethod? acquisitionMethod, bool isPlaceholder,
            DateTime? purchaseDate, decimal? purchasePrice, decimal? marketPriceAtEntry = null,
            string? rarityName = null)
        {
            if (await _collectionRepository.ExistsAsync(imageID, setCode))
                return false;

            var entry = new CollectionEntry
            {
                AcquisitionMethod = acquisitionMethod,
                CardID = cardID,
                Condition = condition,
                DateCreated = DateTime.UtcNow,
                DateModified = DateTime.UtcNow,
                Edition = edition,
                ImageID = imageID,
                IsPlaceholder = isPlaceholder,
                MarketPriceAtEntry = marketPriceAtEntry,
                PurchaseDate = purchaseDate,
                PurchasePrice = purchasePrice,
                Quantity = quantity < 1 ? 1 : quantity,
                RarityName = string.IsNullOrWhiteSpace(rarityName) ? null : rarityName,
                SetCode = setCode,
                Status = status
            };

            await _collectionRepository.AddAsync(entry);
            return true;
        }

        public async Task<(decimal TotalValue, int CardCount, List<(string Label, decimal Value)> SetValueBreakdown)> CalculateCurrentMarketValueAsync()
        {
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");

            var latestSnapshot = await _collectionValueRepository.GetLatestSnapshotAsync();
            if (latestSnapshot is not null && latestSnapshot.SnapshotDate == today)
            {
                var cachedEntrySnapshots = (await _collectionEntryValueRepository.GetLatestSnapshotsAsync()).ToList();
                var cachedSetBreakdown = cachedEntrySnapshots
                    .GroupBy(s => s.SetName)
                    .Select(g => (g.Key, g.Sum(s => s.MarketValue)))
                    .OrderByDescending(x => x.Item2)
                    .Take(20)
                    .ToList();

                return (latestSnapshot.TotalValue, latestSnapshot.CardCount, cachedSetBreakdown);
            }

            var entries = (await _collectionRepository.GetByStatusAsync(CollectionStatus.Owned)).ToList();

            var uniquePrintingKeys = entries
                .Where(e => !string.IsNullOrWhiteSpace(e.RarityName))
                .GroupBy(e => (e.CardID, e.SetCode, RarityName: e.RarityName!))
                .Select(g => g.Key)
                .ToList();

            var priceCache = new Dictionary<(int CardID, string SetCode, string RarityName), decimal?>();
            foreach (var key in uniquePrintingKeys)
            {
                var price = await _pricingService.GetPrintingPriceAsync(key.CardID, key.SetCode, key.RarityName);
                priceCache[key] = price;
                await Task.Delay(_pricingDelayMs);
            }

            var setNamesByCode = _cardDataRepository.GetSetNamesByCode();
            var cardNames = entries
                .Where(e => !string.IsNullOrWhiteSpace(e.RarityName))
                .Select(e => e.CardID)
                .Distinct()
                .ToDictionary(id => id, id => _cardDataRepository.GetCardByID(id)?.Name ?? string.Empty);
            var entrySnapshots = new List<CollectionEntryValueSnapshot>();
            decimal totalValue = 0m;

            foreach (var entry in entries.Where(e => !string.IsNullOrWhiteSpace(e.RarityName)))
            {
                var key = (entry.CardID, entry.SetCode, entry.RarityName!);
                if (!priceCache.TryGetValue(key, out var price) || !price.HasValue)
                    continue;

                var entryValue = price.Value * entry.Quantity;
                totalValue += entryValue;

                entrySnapshots.Add(new CollectionEntryValueSnapshot
                {
                    CardName = cardNames[entry.CardID],
                    CollectionEntryID = entry.ID,
                    DateCreated = DateTime.UtcNow,
                    MarketValue = entryValue,
                    RarityName = entry.RarityName!,
                    SetCode = entry.SetCode,
                    SetName = setNamesByCode.TryGetValue(entry.SetCode, out var n) ? n : entry.SetCode,
                    SnapshotDate = today
                });
            }

            await _collectionEntryValueRepository.UpsertSnapshotsAsync(entrySnapshots, today);

            await _collectionValueRepository.UpsertSnapshotAsync(new CollectionValueSnapshot
            {
                CardCount = entries.Count,
                DateCreated = DateTime.UtcNow,
                SnapshotDate = today,
                TotalValue = totalValue
            });

            var setValueBreakdown = entrySnapshots
                .GroupBy(s => s.SetName)
                .Select(g => (g.Key, g.Sum(s => s.MarketValue)))
                .OrderByDescending(x => x.Item2)
                .Take(20)
                .ToList();

            return (totalValue, entries.Count, setValueBreakdown);
        }

        public Card? GetCardByID(int cardID) => _cardDataRepository.GetCardByID(cardID);

        public IEnumerable<string> GetCardNameSuggestions(string query, int maxResults = 10) =>
            _cardDataRepository.GetBrowseableCards()
                .Where(c => c.Name?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
                .OrderBy(c => c.Name)
                .Take(maxResults)
                .Select(c => c.Name!);

        public async Task<CollectionStatsViewModel> GetCollectionStatsAsync()
        {
            var ownedEntries = (await _collectionRepository.GetByStatusAsync(CollectionStatus.Owned)).ToList();
            var setNamesByCode = _cardDataRepository.GetSetNamesByCode();

            var rarityBreakdown = ownedEntries
                .GroupBy(e => string.IsNullOrWhiteSpace(e.RarityName) ? "Unknown" : e.RarityName)
                .Select(g => (g.Key, g.Count()))
                .OrderByDescending(x => x.Item2)
                .ToList();

            var setBreakdown = ownedEntries
                .GroupBy(e => setNamesByCode.TryGetValue(e.SetCode, out var name) ? name : e.SetCode)
                .Select(g => (g.Key, g.Count()))
                .OrderByDescending(x => x.Item2)
                .ToList();

            var acquisitionBreakdown = ownedEntries
                .GroupBy(e => e.AcquisitionMethod.HasValue
                    ? e.AcquisitionMethod.Value.GetDisplayName()
                    : "Unknown")
                .Select(g => (g.Key, g.Count()))
                .OrderByDescending(x => x.Item2)
                .ToList();

            var latestEntrySnapshots = await _collectionEntryValueRepository.GetLatestSnapshotsAsync();
            var setValueBreakdown = latestEntrySnapshots
                .GroupBy(s => s.SetName)
                .Select(g => (g.Key, g.Sum(s => s.MarketValue)))
                .OrderByDescending(x => x.Item2)
                .Take(20)
                .ToList();

            var valueHistory = await _collectionValueRepository.GetAllSnapshotsAsync();

            return new CollectionStatsViewModel
            {
                AcquisitionBreakdown = acquisitionBreakdown,
                RarityBreakdown = rarityBreakdown,
                SetBreakdown = setBreakdown,
                SetValueBreakdown = setValueBreakdown,
                ValueHistory = valueHistory.ToList()
            };
        }

        public async Task<DashboardStats> GetDashboardStatsAsync()
        {
            var totalArtworks = _cardDataRepository.GetBrowseableArtworks().Count();
            var groups = await GetGroupedOwnedAsync();
            var ordered = await _collectionRepository.GetByStatusAsync(CollectionStatus.Ordered);
            var ownedStats = await _collectionRepository.GetOwnedStatsAsync();
            var latestSnapshot = await _collectionValueRepository.GetLatestSnapshotAsync();

            var collectedPairs = await _collectionRepository.GetCollectedPairsAsync();
            var allPreferred = await _preferredVersionRepository.GetAllAsync();
            var wishlistCount = allPreferred.Count(pv => !collectedPairs.Contains((pv.ImageID, pv.SetCode)));

            return new DashboardStats
            {
                CurrentMarketValue = latestSnapshot?.TotalValue,
                CurrentMarketValueDate = latestSnapshot?.SnapshotDate,
                IncompleteSetCount = groups.Count(g => g.CompletionStatus == CollectionCompletionStatus.Incomplete),
                CompletedCount = groups.Count(g => g.CompletionStatus == CollectionCompletionStatus.Complete),
                OrderedCount = ordered.Count(),
                PlaceholderSetCount = groups.Count(g => g.CompletionStatus == CollectionCompletionStatus.Placeholder),
                TotalArtworks = totalArtworks,
                TotalCardQuantity = ownedStats.TotalQuantity,
                TotalSpent = ownedStats.TotalSpent,
                WishlistCount = wishlistCount
            };
        }

        public Task<IEnumerable<OrderEntryViewModel>> GetEnrichedOrdersAsync()
            => GetEnrichedByStatusAsync(CollectionStatus.Ordered);

        public Task<IEnumerable<OrderEntryViewModel>> GetEnrichedOwnedAsync()
            => GetEnrichedByStatusAsync(CollectionStatus.Owned);

        public async Task<IEnumerable<CollectionGroupViewModel>> GetGroupedOwnedAsync()
        {
            var entries = (await GetEnrichedByStatusAsync(CollectionStatus.Owned)).ToList();

            var imageIDs = entries.Select(e => e.ImageID).Distinct().ToHashSet();
            var preferredVersions = await _preferredVersionRepository.GetByImageIDsAsync(imageIDs);

            return entries
                .GroupBy(e => (e.CardName, e.ImageURLSmall, e.SetCode, e.SetName, e.RarityCode))
                .Select(g =>
                {
                    var first = g.First();
                    var withPrice = g.Where(e => e.PurchasePrice.HasValue).ToList();
                    decimal? totalCost = withPrice.Any()
                        ? withPrice.Sum(e => e.Quantity * e.PurchasePrice!.Value)
                        : null;

                    var isPreferred = preferredVersions.TryGetValue(first.ImageID, out var pv)
                                      && pv.SetCode == first.SetCode;

                    return CollectionGroupViewModel.From(
                        printing: first,
                        entries: g.ToList(),
                        isPreferredVersion: isPreferred,
                        totalCost: totalCost,
                        totalQuantity: g.Sum(e => e.Quantity));
                })
                .OrderBy(g => g.CardName)
                .ThenBy(g => g.SetCode)
                .ToList();
        }

        public async Task<PreferredVersion?> GetPreferredVersionByImageIDAsync(int imageID)
        {
            var dict = await _preferredVersionRepository.GetByImageIDsAsync(new[] { imageID });
            return dict.TryGetValue(imageID, out var pv) ? pv : null;
        }

        public async Task<(Card Card, Image Image)?> GetRandomUncollectedAsync()
        {
            var preferredImageIDs = await _preferredVersionRepository.GetPreferredImageIDsAsync();

            var uncollected = _cardDataRepository.GetBrowseableArtworks()
                .Where(a => !preferredImageIDs.Contains(a.Image.ID))
                .ToList();

            if (uncollected.Count == 0)
                return null;

            var index = Random.Shared.Next(uncollected.Count);
            return uncollected[index];
        }

        public async Task<IEnumerable<WishlistItemViewModel>> GetWishlistAsync()
        {
            var allPreferred = (await _preferredVersionRepository.GetAllAsync()).ToList();
            if (allPreferred.Count == 0)
                return [];

            var collectedPairs = await _collectionRepository.GetCollectedPairsAsync();

            var wishlistItems = allPreferred
                .Where(pv => !collectedPairs.Contains((pv.ImageID, pv.SetCode)))
                .ToList();

            var results = new List<WishlistItemViewModel>();
            foreach (var pv in wishlistItems)
            {
                var printing = BuildCardPrinting(pv.CardID, pv.ImageID, pv.SetCode, pv.RarityName);
                results.Add(WishlistItemViewModel.From(printing));
            }

            return results.OrderBy(r => r.CardName).ThenBy(r => r.SetCode);
        }

        public async Task RemoveFromWishlistAsync(int imageID) =>
            await _preferredVersionRepository.DeleteAsync(imageID);

        public async Task SavePreferredVersionAsync(int cardID, int imageID, string setCode, string? rarityName = null) =>
            await _preferredVersionRepository.AddOrUpdateAsync(cardID, imageID, setCode, rarityName);

        public async Task<PagedResult<CardListItemViewModel>> SearchCardsAsync(BrowseSearchCriteria criteria)
        {
            var query = criteria.Query;
            var page = criteria.Page;
            var pageSize = criteria.PageSize;

            var filtered = _cardDataRepository.GetBrowseableArtworks()
                .Where(a => string.IsNullOrWhiteSpace(query) ||
                            a.Card.Name?.Contains(query, StringComparison.OrdinalIgnoreCase) == true);

            if (!string.IsNullOrWhiteSpace(criteria.CardType))
                filtered = filtered.Where(a => a.Card.CardType?.Contains(criteria.CardType, StringComparison.OrdinalIgnoreCase) == true);

            if (!string.IsNullOrWhiteSpace(criteria.Attribute))
                filtered = filtered.Where(a => a.Card.Attribute == criteria.Attribute);

            if (!string.IsNullOrWhiteSpace(criteria.RarityName))
                filtered = filtered.Where(a => a.Card.CardSets?.Any(s => s.RarityName == criteria.RarityName) == true);

            if (criteria.LevelMin.HasValue)
                filtered = filtered.Where(a => a.Card.Level >= criteria.LevelMin);

            if (criteria.LevelMax.HasValue)
                filtered = filtered.Where(a => a.Card.Level <= criteria.LevelMax);

            var orderedFiltered = filtered.OrderBy(a => a.Card.Name).ThenBy(a => a.Image.ID).ToList();

            var totalCount = orderedFiltered.Count;
            var slice = orderedFiltered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            var imageIDs = slice.Select(a => a.Image.ID).ToList();
            var statusMap = await _collectionRepository.GetStatusByImageIDsAsync(imageIDs);
            var placeholderIDs = await _collectionRepository.GetPlaceholderImageIDsAsync(imageIDs);

            var items = slice.Select(a => new CardListItemViewModel
            {
                Attribute = a.Card.Attribute ?? string.Empty,
                CardID = a.Card.ID,
                CardType = a.Card.CardType ?? string.Empty,
                ImageID = a.Image.ID,
                ImageURLSmall = a.Image.ImageURLSmall ?? string.Empty,
                IsPlaceholder = placeholderIDs.Contains(a.Image.ID),
                Name = a.Card.Name ?? string.Empty,
                Status = statusMap.TryGetValue(a.Image.ID, out var s) ? s : null,
                Type = a.Card.Type ?? string.Empty
            }).ToList();

            return new PagedResult<CardListItemViewModel>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public async Task<PagedResult<CollectionGroupViewModel>> SearchGroupedOwnedAsync(string? query, int page, int pageSize)
        {
            var allGroups = (await GetGroupedOwnedAsync()).ToList();

            var filtered = allGroups
                .Where(g => string.IsNullOrWhiteSpace(query) ||
                            g.CardName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                            g.SetName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                            g.SetCode.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                            g.RarityCode.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var totalCount = filtered.Count;
            var items = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return new PagedResult<CollectionGroupViewModel>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public async Task<WishlistSearchResult> SearchWishlistAsync(string? query, int page, int pageSize, WishlistSortBy sortBy = WishlistSortBy.Name, bool sortDescending = false)
        {
            var allItems = (await GetWishlistAsync()).ToList();

            var filtered = allItems
                .Where(item => string.IsNullOrWhiteSpace(query) ||
                               item.CardName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                               item.SetName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                               item.SetCode.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                               item.RarityName.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();

            IEnumerable<WishlistItemViewModel> sorted = sortBy switch
            {
                WishlistSortBy.Name => sortDescending
                    ? filtered.OrderByDescending(i => i.CardName).ThenBy(i => i.SetCode)
                    : filtered.OrderBy(i => i.CardName).ThenBy(i => i.SetCode),
                _ => filtered.OrderBy(i => i.CardName).ThenBy(i => i.SetCode)
            };

            var sortedList = sorted.ToList();
            var totalCount = sortedList.Count;
            var items = sortedList.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return new WishlistSearchResult
            {
                PagedItems = new PagedResult<WishlistItemViewModel>
                {
                    Items = items,
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalCount
                }
            };
        }

        private CardPrinting BuildCardPrinting(int cardID, int imageID, string setCode, string? rarityNameHint)
        {
            var card = _cardDataRepository.GetCardByID(cardID);
            var image = card?.CardImages?.FirstOrDefault(i => i.ID == imageID);
            var set = rarityNameHint != null
                ? (card?.CardSets?.FirstOrDefault(s => s.Code == setCode && s.RarityName == rarityNameHint)
                   ?? card?.CardSets?.FirstOrDefault(s => s.Code == setCode))
                : card?.CardSets?.FirstOrDefault(s => s.Code == setCode);
            var availableRarities = card?.CardSets?
                .Where(s => s.Code == setCode && !string.IsNullOrEmpty(s.RarityName))
                .Select(s => s.RarityName!)
                .Distinct()
                .OrderBy(r => r)
                .ToList() ?? [];

            return new CardPrinting
            {
                AvailableRarities = availableRarities,
                CardID = cardID,
                CardName = card?.Name ?? "Unknown",
                ImageID = imageID,
                ImageURLSmall = image?.ImageURLSmall ?? string.Empty,
                Price = set?.Price,
                RarityCode = set?.RarityCode ?? string.Empty,
                RarityName = rarityNameHint ?? set?.RarityName ?? string.Empty,
                SetCode = setCode,
                SetName = set?.Name ?? setCode
            };
        }

        private async Task<IEnumerable<OrderEntryViewModel>> GetEnrichedByStatusAsync(CollectionStatus status)
        {
            var entries = await _collectionRepository.GetByStatusAsync(status);
            var viewModels = new List<OrderEntryViewModel>();

            foreach (var entry in entries)
            {
                var printing = BuildCardPrinting(entry.CardID, entry.ImageID, entry.SetCode, entry.RarityName);
                viewModels.Add(OrderEntryViewModel.From(printing, entry));
            }

            return viewModels;
        }
    }
}
