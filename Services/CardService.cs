using CardCollector.Data.Models;
using CardCollector.DTO;
using CardCollector.Repository;
using CardCollector.ViewModels;

namespace CardCollector.Services
{
    public class CardService : ICardService
    {
        private readonly ICardDataRepository _cardDataRepository;
        private readonly ICollectionRepository _collectionRepository;
        private readonly IPreferredVersionRepository _preferredVersionRepository;

        public CardService(
            ICardDataRepository cardDataRepository,
            ICollectionRepository collectionRepository,
            IPreferredVersionRepository preferredVersionRepository)
        {
            _cardDataRepository = cardDataRepository;
            _collectionRepository = collectionRepository;
            _preferredVersionRepository = preferredVersionRepository;
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
                IsPlaceholder = false,
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

        public Card? GetCardByID(int cardID) => _cardDataRepository.GetCardByID(cardID);

        public IEnumerable<string> GetCardNameSuggestions(string query, int maxResults = 10) =>
            _cardDataRepository.GetBrowseableCards()
                .Where(c => c.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                .OrderBy(c => c.Name)
                .Take(maxResults)
                .Select(c => c.Name);

        public async Task<DashboardStats> GetDashboardStatsAsync()
        {
            var totalArtworks = _cardDataRepository.GetBrowseableArtworks().Count();
            var groups = await GetGroupedOwnedAsync();
            var ordered = await _collectionRepository.GetByStatusAsync(CollectionStatus.Ordered);

            return new DashboardStats
            {
                IncompleteSetCount = groups.Count(g => g.CompletionStatus == CollectionCompletionStatus.Incomplete),
                OwnedCount = groups.Count(g => g.CompletionStatus == CollectionCompletionStatus.Complete),
                OrderedCount = ordered.Count(),
                PlaceholderSetCount = groups.Count(g => g.CompletionStatus == CollectionCompletionStatus.Placeholder),
                TotalArtworks = totalArtworks
            };
        }

        public Task<IEnumerable<OrderEntryViewModel>> GetEnrichedOrdersAsync()
            => GetEnrichedByStatusAsync(CollectionStatus.Ordered);

        public Task<IEnumerable<OrderEntryViewModel>> GetEnrichedOwnedAsync()
            => GetEnrichedByStatusAsync(CollectionStatus.Owned);

        public async Task<IEnumerable<CollectionGroupViewModel>> GetGroupedOwnedAsync()
        {
            var entries = (await GetEnrichedByStatusAsync(CollectionStatus.Owned)).ToList();

            var imageIDs = entries.Select(e => e.ImageID).Distinct();
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

        public async Task RemoveFromWishlistAsync(int imageID) =>
            await _preferredVersionRepository.DeleteAsync(imageID);

        public async Task SavePreferredVersionAsync(int cardID, int imageID, string setCode) =>
            await _preferredVersionRepository.AddOrUpdateAsync(cardID, imageID, setCode);

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

        public async Task<PagedResult<CardListItemViewModel>> SearchCardsAsync(BrowseSearchCriteria criteria)
        {
            var query = criteria.Query;
            var page = criteria.Page;
            var pageSize = criteria.PageSize;

            var filtered = _cardDataRepository.GetBrowseableArtworks()
                .Where(a => string.IsNullOrWhiteSpace(query) ||
                            a.Card.Name.Contains(query, StringComparison.OrdinalIgnoreCase));

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

            var wishlistTotal = filtered.Sum(i => i.Price ?? 0m);

            IEnumerable<WishlistItemViewModel> sorted = (sortBy, sortDescending) switch
            {
                (WishlistSortBy.Price, false) => filtered.OrderBy(i => i.Price ?? 0).ThenBy(i => i.CardName),
                (WishlistSortBy.Price, true)  => filtered.OrderByDescending(i => i.Price ?? 0).ThenBy(i => i.CardName),
                (_, true)                     => filtered.OrderByDescending(i => i.CardName).ThenBy(i => i.SetCode),
                _                             => filtered.OrderBy(i => i.CardName).ThenBy(i => i.SetCode)
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
                },
                WishlistTotal = wishlistTotal
            };
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
                var printing = BuildCardPrinting(pv.CardID, pv.ImageID, pv.SetCode, null);
                results.Add(WishlistItemViewModel.From(printing));
            }

            return results.OrderBy(r => r.CardName).ThenBy(r => r.SetCode);
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
                .Select(s => s.RarityName)
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
