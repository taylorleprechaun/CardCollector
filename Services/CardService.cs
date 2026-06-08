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
        private const string SnapshotDateFormat = "yyyy-MM-dd";
        private const int SetBreakdownItemLimit = 20;

        private readonly ICardDataRepository _cardDataRepository;
        private readonly ICardSetRepository _cardSetRepository;
        private readonly ICollectionEntryValueRepository _collectionEntryValueRepository;
        private readonly ICollectionRepository _collectionRepository;
        private readonly ICollectionValueRepository _collectionValueRepository;
        private readonly IDismissedNewPrintingRepository _dismissedNewPrintingRepository;
        private readonly IPreferredVersionRepository _preferredVersionRepository;
        private readonly int _pricingDelayMs;
        private readonly IPricingService _pricingService;

        public CardService(
            ICardDataRepository cardDataRepository,
            ICardSetRepository cardSetRepository,
            ICollectionRepository collectionRepository,
            ICollectionEntryValueRepository collectionEntryValueRepository,
            ICollectionValueRepository collectionValueRepository,
            IDismissedNewPrintingRepository dismissedNewPrintingRepository,
            IPreferredVersionRepository preferredVersionRepository,
            IPricingService pricingService,
            IConfiguration config)
        {
            _cardDataRepository = cardDataRepository;
            _cardSetRepository = cardSetRepository;
            _collectionEntryValueRepository = collectionEntryValueRepository;
            _collectionRepository = collectionRepository;
            _collectionValueRepository = collectionValueRepository;
            _dismissedNewPrintingRepository = dismissedNewPrintingRepository;
            _preferredVersionRepository = preferredVersionRepository;
            _pricingDelayMs = config.GetValue<int>("CardDataSettings:PricingDelayMs", 100);
            _pricingService = pricingService;
        }

        public async Task AddEntryAsync(
            int cardID, int imageID, string setCode, CollectionStatus status,
            int quantity, CardCondition? condition, CardEdition? edition,
            AcquisitionMethod? acquisitionMethod, bool isPlaceholder,
            DateTime? purchaseDate, decimal? purchasePrice, decimal? marketPriceAtEntry = null,
            string? rarityName = null)
        {
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

            await _collectionRepository.AddAsync(entry).ConfigureAwait(false);
        }

        public async Task<(decimal TotalValue, int CardCount, IReadOnlyList<(string Label, decimal Value)> SetValueBreakdown)> CalculateCurrentMarketValueAsync()
        {
            var today = DateTime.UtcNow.ToString(SnapshotDateFormat);

            var latestSnapshot = await _collectionValueRepository.GetLatestSnapshotAsync().ConfigureAwait(false);
            if (latestSnapshot is not null && latestSnapshot.SnapshotDate == today)
            {
                var cachedEntrySnapshots = (await _collectionEntryValueRepository.GetLatestSnapshotsAsync().ConfigureAwait(false)).ToList();
                var cachedSetBreakdown = cachedEntrySnapshots
                    .GroupBy(s => s.SetName)
                    .Select(g => (g.Key, g.Sum(s => s.MarketValue)))
                    .OrderByDescending(x => x.Item2)
                    .Take(SetBreakdownItemLimit)
                    .ToList();

                return (latestSnapshot.TotalValue, latestSnapshot.CardCount, cachedSetBreakdown);
            }

            var entries = (await _collectionRepository.GetByStatusAsync(CollectionStatus.Owned).ConfigureAwait(false)).ToList();

            var uniquePrintingKeys = entries
                .Where(e => !string.IsNullOrWhiteSpace(e.RarityName))
                .GroupBy(e => (e.CardID, e.SetCode, RarityName: e.RarityName!))
                .Select(g => g.Key)
                .ToList();

            var priceCache = new Dictionary<(int CardID, string SetCode, string RarityName), decimal?>();
            foreach (var key in uniquePrintingKeys)
            {
                var price = await _pricingService.GetPrintingPriceAsync(key.CardID, key.SetCode, key.RarityName).ConfigureAwait(false);
                priceCache[key] = price;
                await Task.Delay(_pricingDelayMs).ConfigureAwait(false);
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

            await _collectionEntryValueRepository.UpsertSnapshotsAsync(entrySnapshots, today).ConfigureAwait(false);

            await _collectionValueRepository.UpsertSnapshotAsync(new CollectionValueSnapshot
            {
                CardCount = entries.Count,
                DateCreated = DateTime.UtcNow,
                SnapshotDate = today,
                TotalValue = totalValue
            }).ConfigureAwait(false);

            var setValueBreakdown = entrySnapshots
                .GroupBy(s => s.SetName)
                .Select(g => (g.Key, g.Sum(s => s.MarketValue)))
                .OrderByDescending(x => x.Item2)
                .Take(SetBreakdownItemLimit)
                .ToList();

            return (totalValue, entries.Count, setValueBreakdown);
        }

        public async Task DismissNewPrintingAsync(int cardID, string setCode, string rarityName) =>
            await _dismissedNewPrintingRepository.AddAsync(cardID, setCode, rarityName).ConfigureAwait(false);

        public Card? GetCardByID(int cardID) => _cardDataRepository.GetCardByID(cardID);

        public IEnumerable<string> GetCardNameSuggestions(string query, int maxResults = 10) =>
            _cardDataRepository.GetBrowseableCards()
                .Where(c => c.Name?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
                .OrderBy(c => c.Name)
                .Take(maxResults)
                .Select(c => c.Name!);

        public async Task<CollectionStatsViewModel> GetCollectionStatsAsync()
        {
            var ownedEntries = (await _collectionRepository.GetByStatusAsync(CollectionStatus.Owned).ConfigureAwait(false)).ToList();
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

            var latestEntrySnapshots = await _collectionEntryValueRepository.GetLatestSnapshotsAsync().ConfigureAwait(false);
            var setValueBreakdown = latestEntrySnapshots
                .GroupBy(s => s.SetName)
                .Select(g => (g.Key, g.Sum(s => s.MarketValue)))
                .OrderByDescending(x => x.Item2)
                .Take(SetBreakdownItemLimit)
                .ToList();

            var valueHistory = await _collectionValueRepository.GetAllSnapshotsAsync().ConfigureAwait(false);

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
            var totalCards = _cardDataRepository.GetBrowseableCards().Count();
            var groups = await GetGroupedOwnedAsync().ConfigureAwait(false);
            var ordered = await _collectionRepository.GetByStatusAsync(CollectionStatus.Ordered).ConfigureAwait(false);
            var ownedStats = await _collectionRepository.GetOwnedStatsAsync().ConfigureAwait(false);
            var latestSnapshot = await _collectionValueRepository.GetLatestSnapshotAsync().ConfigureAwait(false);

            var collectedPairs = await _collectionRepository.GetCollectedPairsAsync().ConfigureAwait(false);
            var allPreferred = await _preferredVersionRepository.GetAllAsync().ConfigureAwait(false);
            var wishlistCount = allPreferred.Count(pv => !collectedPairs.Contains((pv.ImageID, pv.SetCode)));

            return new DashboardStats
            {
                CurrentMarketValue = latestSnapshot?.TotalValue,
                CurrentMarketValueDate = latestSnapshot?.SnapshotDate,
                IncompleteSetCount = groups.Count(g => g.CompletionStatus == CollectionCompletionStatus.Incomplete),
                CompletedCount = groups.Count(g => g.CompletionStatus == CollectionCompletionStatus.Complete),
                OrderedCount = ordered.Count(),
                PlaceholderSetCount = groups.Count(g => g.CompletionStatus == CollectionCompletionStatus.Placeholder),
                TotalCards = totalCards,
                TotalCardQuantity = ownedStats.TotalQuantity,
                TotalSpent = ownedStats.TotalSpent,
                WishlistCount = wishlistCount
            };
        }

        public Task<IEnumerable<OrderEntryViewModel>> GetEnrichedOrdersAsync()
            => GetEnrichedByStatusAsync(CollectionStatus.Ordered);

        public Task<IEnumerable<OrderEntryViewModel>> GetEnrichedOwnedAsync()
            => GetEnrichedByStatusAsync(CollectionStatus.Owned);

        public async Task<IEnumerable<CollectionEntry>> GetEntriesByCardIDAsync(int cardID) =>
            await _collectionRepository.GetByCardIDAsync(cardID).ConfigureAwait(false);

        public async Task<IEnumerable<CollectionGroupViewModel>> GetGroupedOwnedAsync()
        {
            var entries = (await GetEnrichedByStatusAsync(CollectionStatus.Owned).ConfigureAwait(false)).ToList();

            var imageIDs = entries.Select(e => e.ImageID).Distinct().ToHashSet();
            var preferredVersions = await _preferredVersionRepository.GetByImageIDsAsync(imageIDs).ConfigureAwait(false);

            return entries
                .GroupBy(e => (e.CardName, e.SetCode, e.SetName, e.RarityCode))
                .Select(g =>
                {
                    var first = g.First();
                    var withPrice = g.Where(e => e.PurchasePrice.HasValue).ToList();
                    decimal? totalCost = withPrice.Any()
                        ? withPrice.Sum(e => e.Quantity * e.PurchasePrice!.Value)
                        : null;

                    var isPreferred = g.Select(e => e.ImageID).Distinct().Any(imgID =>
                        preferredVersions.TryGetValue(imgID, out var pv)
                        && pv.SetCode.Equals(first.SetCode, StringComparison.OrdinalIgnoreCase)
                        && (pv.RarityName is null || pv.RarityName.Equals(first.RarityName, StringComparison.OrdinalIgnoreCase)));

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

        public async Task<IReadOnlyList<NewPrintingOpportunityViewModel>> GetNewPrintingOpportunitiesAsync()
        {
            var preferredVersions = (await _preferredVersionRepository.GetAllAsync().ConfigureAwait(false)).ToList();
            var dismissed = await _dismissedNewPrintingRepository.GetAllAsync().ConfigureAwait(false);
            var setNamesByCode = _cardDataRepository.GetSetNamesByCode();
            var result = new List<NewPrintingOpportunityViewModel>();
            var today = DateTime.UtcNow.ToString(SnapshotDateFormat);

            foreach (var pv in preferredVersions)
            {
                var card = _cardDataRepository.GetCardByID(pv.CardID);
                if (card?.CardSets is null || card.CardImages is null)
                    continue;

                var preferredDate = _cardSetRepository.GetTCGDateBySetCode(pv.SetCode);
                if (preferredDate is null)
                    continue;

                var image = card.CardImages.FirstOrDefault(i => i.ID == pv.ImageID);

                var newerPrintings = card.CardSets
                    .Where(s => s.Code != pv.SetCode || s.RarityName != pv.RarityName)
                    .Select(s => (Set: s, Date: _cardSetRepository.GetTCGDateBySetCode(s.Code ?? string.Empty)))
                    .Where(x => x.Date is not null
                                && string.Compare(x.Date, preferredDate, StringComparison.Ordinal) > 0
                                && string.Compare(x.Date, today, StringComparison.Ordinal) <= 0
                                && !dismissed.Contains((pv.CardID, x.Set.Code ?? string.Empty, x.Set.RarityName ?? string.Empty)))
                    .Select(x => new NewPrintingOptionViewModel
                    {
                        RarityName = x.Set.RarityName ?? string.Empty,
                        ReleaseDate = x.Date,
                        SetCode = x.Set.Code ?? string.Empty,
                        SetName = setNamesByCode.TryGetValue(x.Set.Code ?? string.Empty, out var sn) ? sn : x.Set.Name ?? string.Empty
                    })
                    .OrderBy(o => o.ReleaseDate)
                    .ThenBy(o => o.SetCode)
                    .ToList();

                if (newerPrintings.Count == 0)
                    continue;

                result.Add(new NewPrintingOpportunityViewModel
                {
                    CardID = pv.CardID,
                    CardName = card.Name ?? string.Empty,
                    CurrentRarityName = pv.RarityName ?? string.Empty,
                    CurrentReleaseDate = preferredDate,
                    CurrentSetCode = pv.SetCode,
                    CurrentSetName = setNamesByCode.TryGetValue(pv.SetCode, out var csn) ? csn : pv.SetCode,
                    ImageID = pv.ImageID,
                    ImageURLSmall = image?.ImageURLSmall ?? string.Empty,
                    NewerPrintings = newerPrintings
                });
            }

            return result.OrderBy(r => r.CardName).ToList();
        }

        public async Task<PreferredVersion?> GetPreferredVersionByCardIDAsync(int cardID) =>
            await _preferredVersionRepository.GetByCardIDAsync(cardID).ConfigureAwait(false);

        public async Task<Card?> GetRandomUncollectedAsync()
        {
            var preferredCardIDs = await _preferredVersionRepository.GetPreferredCardIDsAsync().ConfigureAwait(false);

            var uncollected = _cardDataRepository.GetBrowseableCards()
                .Where(c => !preferredCardIDs.Contains(c.ID))
                .ToList();

            if (uncollected.Count == 0)
                return null;

            var index = Random.Shared.Next(uncollected.Count);
            return uncollected[index];
        }

        public async Task<IEnumerable<WishlistItemViewModel>> GetWishlistAsync()
        {
            var allPreferred = (await _preferredVersionRepository.GetAllAsync().ConfigureAwait(false)).ToList();
            if (allPreferred.Count == 0)
                return [];

            var collectedPairs = await _collectionRepository.GetCollectedPairsAsync().ConfigureAwait(false);

            var notCollected = allPreferred
                .Where(pv => !collectedPairs.Contains((pv.ImageID, pv.SetCode)))
                .ToList();

            var partiallyCollected = allPreferred
                .Where(pv => collectedPairs.Contains((pv.ImageID, pv.SetCode)))
                .ToList();

            var ownedQuantities = partiallyCollected.Count > 0
                ? await _collectionRepository.GetOwnedQuantitiesForPairsAsync(
                    partiallyCollected.Select(pv => (pv.ImageID, pv.SetCode))).ConfigureAwait(false)
                : new Dictionary<(int, string), int>();

            var results = new List<WishlistItemViewModel>();

            foreach (var pv in notCollected)
            {
                var printing = BuildCardPrinting(pv.CardID, pv.ImageID, pv.SetCode, pv.RarityName);
                results.Add(WishlistItemViewModel.From(printing));
            }

            foreach (var pv in partiallyCollected)
            {
                if (!ownedQuantities.TryGetValue((pv.ImageID, pv.SetCode), out var ownedQty))
                    continue;

                if (ownedQty <= 0 || ownedQty >= CardPrinting.CompleteThreshold)
                    continue;

                var printing = BuildCardPrinting(pv.CardID, pv.ImageID, pv.SetCode, pv.RarityName);
                results.Add(WishlistItemViewModel.From(printing, ownedQty));
            }

            return results.OrderBy(r => r.CardName).ThenBy(r => r.SetCode);
        }

        public async Task RemoveFromWishlistAsync(int imageID) =>
            await _preferredVersionRepository.DeleteAsync(imageID).ConfigureAwait(false);

        public async Task SavePreferredVersionAsync(int cardID, int imageID, string setCode, string? rarityName = null) =>
            await _preferredVersionRepository.AddOrUpdateAsync(cardID, imageID, setCode, rarityName).ConfigureAwait(false);

        public async Task<PagedResult<CardListItemViewModel>> SearchCardsAsync(BrowseSearchCriteria criteria)
        {
            var page = criteria.Page;
            var pageSize = criteria.PageSize;

            var setPrefix = string.IsNullOrWhiteSpace(criteria.SetName) ? null : _cardDataRepository.GetSetPrefixByName(criteria.SetName);
            var filtered = ApplyCommonCardFilters(
                _cardDataRepository.GetBrowseableCards(),
                criteria.Query,
                criteria.CardType,
                setPrefix,
                criteria.RarityName);

            var orderedFiltered = filtered.OrderBy(c => c.Name).ToList();

            var totalCount = orderedFiltered.Count;
            var slice = orderedFiltered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            var cardIDs = slice.Select(c => c.ID).ToList();
            var statusMap = await _collectionRepository.GetStatusByCardIDsAsync(cardIDs).ConfigureAwait(false);

            // Roll up completion status from all artworks of each card in the slice.
            var allImageIDs = slice
                .SelectMany(c => c.CardImages?.Select(i => i.ID) ?? [])
                .ToList();
            var imageCompletionMap = await _collectionRepository.GetCompletionStatusByImageIDsAsync(allImageIDs).ConfigureAwait(false);
            var cardCompletionMap = slice.ToDictionary(
                c => c.ID,
                c =>
                {
                    var statuses = (c.CardImages ?? [])
                        .Select(i => imageCompletionMap.TryGetValue(i.ID, out var s) ? s : (CollectionCompletionStatus?)null)
                        .Where(s => s.HasValue)
                        .Select(s => s!.Value)
                        .ToList();
                    if (statuses.Count == 0) return (CollectionCompletionStatus?)null;
                    if (statuses.Contains(CollectionCompletionStatus.Complete)) return CollectionCompletionStatus.Complete;
                    if (statuses.Contains(CollectionCompletionStatus.Incomplete)) return CollectionCompletionStatus.Incomplete;
                    return CollectionCompletionStatus.Placeholder;
                });

            var items = slice.Select(c => new CardListItemViewModel
            {
                Attribute = c.Attribute ?? string.Empty,
                CardID = c.ID,
                CardType = c.CardType ?? string.Empty,
                CompletionStatus = statusMap.TryGetValue(c.ID, out var rawStatus) && rawStatus == CollectionStatus.Owned
                    ? (cardCompletionMap.TryGetValue(c.ID, out var cs) ? cs : null)
                    : null,
                ImageID = c.CardImages?.FirstOrDefault()?.ID ?? c.ID,
                ImageURLSmall = c.CardImages?.FirstOrDefault()?.ImageURLSmall ?? string.Empty,
                Name = c.Name ?? string.Empty,
                Status = statusMap.TryGetValue(c.ID, out var s) ? s : null,
                Type = c.Type ?? string.Empty
            }).ToList();

            return new PagedResult<CardListItemViewModel>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public async Task<PagedResult<CollectionGroupViewModel>> SearchGroupedOwnedAsync(CollectionSearchCriteria criteria)
        {
            var allGroups = (await GetGroupedOwnedAsync().ConfigureAwait(false)).ToList();

            var setPrefix = string.IsNullOrWhiteSpace(criteria.SetName) ? null : _cardDataRepository.GetSetPrefixByName(criteria.SetName);
            var filtered = ApplyCommonPrintingFilters(
                allGroups,
                criteria.Query,
                criteria.CardType,
                setPrefix,
                criteria.RarityName);

            if (criteria.Condition.HasValue)
                filtered = filtered.Where(g => g.Entries.Any(e => e.Condition == criteria.Condition));

            if (criteria.Edition.HasValue)
                filtered = filtered.Where(g => g.Entries.Any(e => e.Edition == criteria.Edition));

            if (criteria.AcquisitionMethod.HasValue)
                filtered = filtered.Where(g => g.Entries.Any(e => e.AcquisitionMethod == criteria.AcquisitionMethod));

            var filteredList = filtered.ToList();
            var totalCount = filteredList.Count;
            var items = filteredList.Skip((criteria.Page - 1) * criteria.PageSize).Take(criteria.PageSize).ToList();

            return new PagedResult<CollectionGroupViewModel>
            {
                Items = items,
                Page = criteria.Page,
                PageSize = criteria.PageSize,
                TotalCount = totalCount
            };
        }

        public async Task<WishlistSearchResult> SearchWishlistAsync(WishlistSearchCriteria criteria)
        {
            var allItems = (await GetWishlistAsync().ConfigureAwait(false)).ToList();

            var setPrefix = string.IsNullOrWhiteSpace(criteria.SetName) ? null : _cardDataRepository.GetSetPrefixByName(criteria.SetName);
            var filtered = ApplyCommonPrintingFilters(
                allItems,
                criteria.Query,
                criteria.CardType,
                setPrefix,
                criteria.RarityName);

            IEnumerable<WishlistItemViewModel> sorted = criteria.SortBy switch
            {
                WishlistSortBy.Name => criteria.SortDescending
                    ? filtered.OrderByDescending(i => i.CardName).ThenBy(i => i.SetCode)
                    : filtered.OrderBy(i => i.CardName).ThenBy(i => i.SetCode),
                _ => filtered.OrderBy(i => i.CardName).ThenBy(i => i.SetCode)
            };

            var sortedList = sorted.ToList();
            var totalCount = sortedList.Count;
            var items = sortedList.Skip((criteria.Page - 1) * criteria.PageSize).Take(criteria.PageSize).ToList();

            return new WishlistSearchResult
            {
                PagedItems = new PagedResult<WishlistItemViewModel>
                {
                    Items = items,
                    Page = criteria.Page,
                    PageSize = criteria.PageSize,
                    TotalCount = totalCount
                }
            };
        }

        public async Task<IReadOnlyList<string>> GetWishlistDistinctRarityNamesAsync()
        {
            var items = await GetWishlistAsync().ConfigureAwait(false);
            return items
                .Select(i => i.RarityName)
                .Where(r => !string.IsNullOrEmpty(r))
                .Distinct()
                .OrderBy(r => r)
                .ToList();
        }

        public async Task<IReadOnlyList<string>> GetWishlistDistinctSetNamesAsync()
        {
            var items = await GetWishlistAsync().ConfigureAwait(false);
            var setNamesByCode = _cardDataRepository.GetSetNamesByCode();
            return items
                .Where(i => !string.IsNullOrEmpty(i.SetCode))
                .Select(i => setNamesByCode.TryGetValue(i.SetCode, out var name) ? name : i.SetName)
                .Where(n => !string.IsNullOrEmpty(n))
                .Distinct()
                .OrderBy(n => n)
                .ToList();
        }

        public async Task UpgradePreferredVersionAsync(int imageID, int cardID, string newSetCode, string newRarityName) =>
            await _preferredVersionRepository.AddOrUpdateAsync(cardID, imageID, newSetCode, newRarityName).ConfigureAwait(false);

        private static IEnumerable<Card> ApplyCommonCardFilters(
            IEnumerable<Card> cards,
            string? query,
            string? cardType,
            string? setPrefix,
            string? rarityName)
        {
            if (!string.IsNullOrWhiteSpace(query))
                cards = cards.Where(c => c.Name?.Contains(query, StringComparison.OrdinalIgnoreCase) == true);

            if (!string.IsNullOrWhiteSpace(cardType))
                cards = cards.Where(c => c.CardType?.Contains(cardType, StringComparison.OrdinalIgnoreCase) == true);

            if (!string.IsNullOrWhiteSpace(setPrefix) || !string.IsNullOrWhiteSpace(rarityName))
                cards = cards.Where(c => c.CardSets?.Any(s =>
                    (string.IsNullOrWhiteSpace(setPrefix) || (s.Code != null && GetSetPrefix(s.Code).Equals(setPrefix, StringComparison.OrdinalIgnoreCase))) &&
                    (string.IsNullOrWhiteSpace(rarityName) || s.RarityName == rarityName)) == true);

            return cards;
        }

        private static IEnumerable<T> ApplyCommonPrintingFilters<T>(
            IEnumerable<T> items,
            string? query,
            string? cardType,
            string? setPrefix,
            string? rarityName) where T : CardPrinting
        {
            if (!string.IsNullOrWhiteSpace(query))
                items = items.Where(i =>
                    i.CardName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    i.SetName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    i.SetCode.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    i.RarityCode.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    i.RarityName.Contains(query, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(cardType))
                items = items.Where(i => i.CardType.Contains(cardType, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(setPrefix))
                items = items.Where(i => GetSetPrefix(i.SetCode).Equals(setPrefix, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(rarityName))
                items = items.Where(i => i.RarityName.Equals(rarityName, StringComparison.OrdinalIgnoreCase));

            return items;
        }

        private static string GetSetPrefix(string code)
        {
            var hyphen = code.IndexOf('-');
            return hyphen > 0 ? code[..hyphen] : code;
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
                CardType = card?.CardType ?? string.Empty,
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
            var entries = await _collectionRepository.GetByStatusAsync(status).ConfigureAwait(false);
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
