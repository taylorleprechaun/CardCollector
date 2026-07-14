using CardCollector.Data.Models;
using CardCollector.DTO;
using CardCollector.Extensions;
using CardCollector.Repository;
using CardCollector.ViewModels;
using Microsoft.Extensions.Logging;

namespace CardCollector.Services
{
    public sealed class CardService : ICardService
    {
        private const string SnapshotDateFormat = "yyyy-MM-dd";
        private const int SetBreakdownItemLimit = 20;

        private readonly ICardDataRepository _cardDataRepository;
        private readonly ICardSetRepository _cardSetRepository;
        private readonly ICheckedOutRepository _checkedOutRepository;
        private readonly ICollectionEntryValueRepository _collectionEntryValueRepository;
        private readonly ICollectionRepository _collectionRepository;
        private readonly ICollectionValueRepository _collectionValueRepository;
        private readonly IDismissedNewPrintingRepository _dismissedNewPrintingRepository;
        private readonly ILogger<CardService> _logger;
        private readonly IPreferredVersionRepository _preferredVersionRepository;
        private readonly IPricingService _pricingService;

        public CardService(
            ICardDataRepository cardDataRepository,
            ICardSetRepository cardSetRepository,
            ICheckedOutRepository checkedOutRepository,
            ICollectionRepository collectionRepository,
            ICollectionEntryValueRepository collectionEntryValueRepository,
            ICollectionValueRepository collectionValueRepository,
            IDismissedNewPrintingRepository dismissedNewPrintingRepository,
            ILogger<CardService> logger,
            IPreferredVersionRepository preferredVersionRepository,
            IPricingService pricingService)
        {
            _cardDataRepository = cardDataRepository;
            _cardSetRepository = cardSetRepository;
            _checkedOutRepository = checkedOutRepository;
            _collectionEntryValueRepository = collectionEntryValueRepository;
            _collectionRepository = collectionRepository;
            _collectionValueRepository = collectionValueRepository;
            _dismissedNewPrintingRepository = dismissedNewPrintingRepository;
            _logger = logger;
            _preferredVersionRepository = preferredVersionRepository;
            _pricingService = pricingService;
        }

        public async Task AddEntryAsync(
            int cardID, int imageID, string setCode, CollectionStatus status,
            int quantity, CardCondition? condition, CardEdition? edition,
            AcquisitionMethod? acquisitionMethod,
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
                MarketPriceAtEntry = marketPriceAtEntry,
                PurchaseDate = purchaseDate,
                PurchasePrice = purchasePrice,
                Quantity = quantity < 1 ? 1 : quantity,
                RarityName = string.IsNullOrWhiteSpace(rarityName) ? null : rarityName,
                SetCode = setCode,
                Status = status
            };

            await _collectionRepository.AddAsync(entry).ConfigureAwait(false);
            await AutoDismissNewPrintingsForCardAsync(cardID, setCode).ConfigureAwait(false);
        }

        public async Task<(decimal TotalValue, int CardCount, IReadOnlyList<(string Label, decimal Value)> SetValueBreakdown, IReadOnlyList<(string CardName, string SetName, string RarityName, decimal Value)> TopValueCards)> CalculateCurrentMarketValueAsync(Func<int, int, Task>? onProgress = null)
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

                var cachedEntries = (await _collectionRepository.GetByStatusAsync(CollectionStatus.Owned).ConfigureAwait(false)).ToList();
                var cachedQuantityByID = cachedEntries.ToDictionary(e => e.ID, e => e.Quantity);
                var cachedTopCards = cachedEntrySnapshots
                    .Where(s => cachedQuantityByID.ContainsKey(s.CollectionEntryID) && cachedQuantityByID[s.CollectionEntryID] > 0)
                    .Select(s => (s.CardName, s.SetName, s.RarityName, UnitPrice: s.MarketValue / cachedQuantityByID[s.CollectionEntryID]))
                    .GroupBy(x => (x.CardName, x.SetName, x.RarityName))
                    .Select(g => (g.Key.CardName, g.Key.SetName, g.Key.RarityName, g.First().UnitPrice))
                    .OrderByDescending(x => x.Item4)
                    .Take(10)
                    .ToList();
                return (latestSnapshot.TotalValue, latestSnapshot.CardCount, cachedSetBreakdown, cachedTopCards);
            }

            var entries = (await _collectionRepository.GetByStatusAsync(CollectionStatus.Owned).ConfigureAwait(false)).ToList();

            var previousSnapshots = (await _collectionEntryValueRepository.GetLatestSnapshotsAsync().ConfigureAwait(false))
                .ToDictionary(s => s.CollectionEntryID, s => s.MarketValue);

            var uniquePrintingKeys = entries
                .Where(e => !string.IsNullOrWhiteSpace(e.RarityName))
                .GroupBy(e => (e.CardID, e.SetCode, RarityName: e.RarityName!, e.Edition))
                .Select(g => g.Key)
                .ToList();

            var priceCache = new Dictionary<(int CardID, string SetCode, string RarityName, CardEdition? Edition), decimal?>();
            var totalPrintings = uniquePrintingKeys.Count;
            var processedPrintings = 0;
            foreach (var key in uniquePrintingKeys)
            {
                var price = await _pricingService.GetPrintingPriceAsync(key.CardID, key.SetCode, key.RarityName, key.Edition).ConfigureAwait(false);
                priceCache[key] = price;
                if (onProgress is not null)
                    await onProgress(++processedPrintings, totalPrintings).ConfigureAwait(false);
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
                var key = (entry.CardID, entry.SetCode, entry.RarityName!, entry.Edition);
                priceCache.TryGetValue(key, out var price);

                decimal entryValue;
                if (price.HasValue)
                {
                    entryValue = price.Value * entry.Quantity;
                }
                else if (previousSnapshots.TryGetValue(entry.ID, out var previousValue))
                {
                    entryValue = previousValue;
                    _logger.LogWarning("Price unavailable for entry {EntryID} ({SetCode} {RarityName}); using previous snapshot value {Value}",
                        entry.ID, entry.SetCode, entry.RarityName, previousValue);
                }
                else
                {
                    continue;
                }

                totalValue += entryValue;

                entrySnapshots.Add(new CollectionEntryValueSnapshot
                {
                    CardName = cardNames[entry.CardID],
                    CollectionEntryID = entry.ID,
                    DateCreated = DateTime.UtcNow,
                    Edition = entry.Edition,
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
                CardCount = entries.Sum(e => e.Quantity),
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

            var quantityByEntryID = entries.ToDictionary(e => e.ID, e => e.Quantity);
            var topValueCards = entrySnapshots
                .Where(s => quantityByEntryID.TryGetValue(s.CollectionEntryID, out var q) && q > 0)
                .Select(s => (s.CardName, s.SetName, s.RarityName, UnitPrice: s.MarketValue / quantityByEntryID[s.CollectionEntryID]))
                .GroupBy(x => (x.CardName, x.SetName, x.RarityName))
                .Select(g => (g.Key.CardName, g.Key.SetName, g.Key.RarityName, g.First().UnitPrice))
                .OrderByDescending(x => x.Item4)
                .Take(10)
                .ToList();

            return (totalValue, entries.Sum(e => e.Quantity), setValueBreakdown, topValueCards);
        }

        public async Task<EditionAuditCategory?> CheckEntryEditionAsync(int cardID, string setCode, string rarityName, CardEdition edition)
        {
            var editionMap = await _pricingService.GetCardEditionMapAsync(cardID).ConfigureAwait(false);
            var (category, _) = CategorizeEdition(editionMap, setCode, rarityName, edition);
            return category;
        }

        public async Task CheckInCardAsync(int imageID, string setCode, string rarityName)
        {
            if (string.IsNullOrWhiteSpace(setCode)) throw new ArgumentException("setCode is required.", nameof(setCode));

            await _checkedOutRepository.RemoveAsync(imageID, setCode, rarityName).ConfigureAwait(false);
        }

        public async Task CheckOutCardAsync(int cardID, int imageID, string setCode, string rarityName, int quantity)
        {
            if (string.IsNullOrWhiteSpace(setCode)) throw new ArgumentException("setCode is required.", nameof(setCode));

            var existing = await _checkedOutRepository.GetAsync(imageID, setCode, rarityName).ConfigureAwait(false);
            if (existing is not null)
            {
                await _checkedOutRepository.UpdateAsync(imageID, setCode, rarityName, quantity).ConfigureAwait(false);
                return;
            }

            var now = DateTime.UtcNow;
            await _checkedOutRepository.AddAsync(new Data.Models.CheckedOutCard
            {
                CardID = cardID,
                CheckedOutDate = now,
                DateCreated = now,
                DateModified = now,
                ImageID = imageID,
                Quantity = quantity,
                RarityName = rarityName,
                SetCode = setCode
            }).ConfigureAwait(false);
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

        public async Task<IReadOnlyList<CardPriceHistorySeries>> GetCardPriceHistoryAsync(string cardName)
        {
            if (string.IsNullOrWhiteSpace(cardName)) throw new ArgumentException("cardName is required.", nameof(cardName));

            var snapshots = (await _collectionEntryValueRepository.GetHistoryByCardNameAsync(cardName).ConfigureAwait(false)).ToList();

            var entries = (await _collectionRepository.GetByStatusAsync(CollectionStatus.Owned).ConfigureAwait(false)).ToList();
            var quantityByEntryID = entries.ToDictionary(e => e.ID, e => e.Quantity);

            return snapshots
                .GroupBy(s => s.CollectionEntryID)
                .Select(g =>
                {
                    var byDate = g.GroupBy(s => s.SnapshotDate).OrderBy(dg => dg.Key).ToList();
                    var latest = g.OrderBy(s => s.SnapshotDate).Last();
                    var editionSuffix = latest.Edition is null ? string.Empty : $" — {latest.Edition.Value.GetDisplayName()}";
                    return new CardPriceHistorySeries
                    {
                        Label = $"{latest.SetCode} — {latest.RarityName}{editionSuffix}",
                        Dates = byDate.Select(dg => dg.Key).ToList(),
                        Values = byDate.Select(dg =>
                        {
                            var totalValue = dg.Sum(s => s.MarketValue);
                            var qty = quantityByEntryID.TryGetValue(g.Key, out var q) && q > 0 ? q : 1;
                            return totalValue / qty;
                        }).ToList()
                    };
                })
                .OrderBy(s => s.Label)
                .ToList();
        }

        public async Task<CollectionStatsViewModel> GetCollectionStatsAsync()
        {
            var ownedEntries = (await _collectionRepository.GetByStatusAsync(CollectionStatus.Owned).ConfigureAwait(false)).ToList();
            var setNamesByCode = _cardDataRepository.GetSetNamesByCode();

            var rarityBreakdown = ownedEntries
                .GroupBy(e => string.IsNullOrWhiteSpace(e.RarityName) ? "Unknown" : e.RarityName)
                .Select(g => (g.Key, g.Count()))
                .OrderByDescending(x => x.Item2)
                .Take(10)
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
                .Select(g => (g.Key, g.Sum(e => e.Quantity)))
                .OrderByDescending(x => x.Item2)
                .ToList();

            var latestEntrySnapshots = (await _collectionEntryValueRepository.GetLatestSnapshotsAsync().ConfigureAwait(false)).ToList();
            var setValueBreakdown = latestEntrySnapshots
                .GroupBy(s => s.SetName)
                .Select(g => (g.Key, g.Sum(s => s.MarketValue)))
                .OrderByDescending(x => x.Item2)
                .Take(SetBreakdownItemLimit)
                .ToList();

            var quantityByID = ownedEntries.ToDictionary(e => e.ID, e => e.Quantity);
            var topValueCards = latestEntrySnapshots
                .Where(s => quantityByID.ContainsKey(s.CollectionEntryID) && quantityByID[s.CollectionEntryID] > 0)
                .Select(s => (s.CardName, s.SetName, s.RarityName, UnitPrice: s.MarketValue / quantityByID[s.CollectionEntryID]))
                .GroupBy(x => (x.CardName, x.SetName, x.RarityName))
                .Select(g => (g.Key.CardName, g.Key.SetName, g.Key.RarityName, g.First().UnitPrice))
                .OrderByDescending(x => x.Item4)
                .Take(10)
                .ToList();

            var valueHistory = await _collectionValueRepository.GetAllSnapshotsAsync().ConfigureAwait(false);

            return new CollectionStatsViewModel
            {
                AcquisitionBreakdown = acquisitionBreakdown,
                RarityBreakdown = rarityBreakdown,
                SetBreakdown = setBreakdown,
                SetValueBreakdown = setValueBreakdown,
                TopValueCards = topValueCards,
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

            var collectedPairs = await _collectionRepository.GetOwnedPairsAsync().ConfigureAwait(false);
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
            var checkedOutLookup = await _checkedOutRepository.GetCheckedOutLookupAsync().ConfigureAwait(false);

            // Phase 1: build all groups; non-preferred groups default PreferredVersionIsComplete = false
            var allGroups = entries
                .GroupBy(e => (e.CardName, e.SetCode, e.SetName, e.RarityName))
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

                    var hasCheckout = checkedOutLookup.TryGetValue((first.ImageID, first.SetCode, first.RarityName), out var checkoutInfo);

                    return CollectionGroupViewModel.From(
                        printing: first,
                        entries: g.ToList(),
                        isPreferredVersion: isPreferred,
                        preferredVersionIsComplete: false,
                        totalCost: totalCost,
                        totalQuantity: g.Sum(e => e.Quantity),
                        checkedOutQuantity: hasCheckout ? checkoutInfo.Quantity : 0,
                        checkedOutDate: hasCheckout ? checkoutInfo.Date : null);
                })
                .ToList();

            // Phase 2: identify imageIDs where the preferred version group is Complete
            var completePreferredImageIDs = allGroups
                .Where(g => g.IsPreferredVersion && g.TotalQuantity >= CardPrinting.CompleteThreshold)
                .Select(g => g.ImageID)
                .ToHashSet();

            // Phase 3: rebuild non-preferred groups with the correct PreferredVersionIsComplete flag
            return allGroups
                .Select(g => g.IsPreferredVersion ? g : CollectionGroupViewModel.From(
                    printing: g,
                    entries: g.Entries,
                    isPreferredVersion: false,
                    preferredVersionIsComplete: completePreferredImageIDs.Contains(g.ImageID),
                    totalCost: g.TotalCost,
                    totalQuantity: g.TotalQuantity,
                    checkedOutQuantity: g.CheckedOutQuantity,
                    checkedOutDate: g.CheckedOutDate))
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

        public async Task<PurchasePlanViewModel> GetPurchasePlanAsync(decimal? totalBudget = null, int? maxCards = null, decimal? maxPricePerCard = null, DateTime? asOfUtc = null)
        {
            var candidates = await GetPurchasePriorityCandidatesAsync(asOfUtc, maxPricePerCard).ConfigureAwait(false);

            var items = new List<PurchasePriorityCandidateViewModel>();
            var remainingBudget = totalBudget;

            foreach (var candidate in candidates)
            {
                if (maxCards.HasValue && items.Count >= maxCards.Value)
                    break;

                // Skip (don't stop) anything that doesn't fit the remaining budget — a cheaper, lower-priority
                // candidate further down the list may still fit and is worth taking.
                if (remainingBudget.HasValue && candidate.LineTotal > remainingBudget.Value)
                    continue;

                items.Add(candidate);
                if (remainingBudget.HasValue)
                    remainingBudget -= candidate.LineTotal;
            }

            return new PurchasePlanViewModel
            {
                Items = items,
                TotalCost = items.Sum(i => i.LineTotal)
            };
        }


        public async Task<IReadOnlyList<PurchasePriorityCandidateViewModel>> GetPurchasePriorityCandidatesAsync(DateTime? asOfUtc = null, decimal? maxPrice = null)
        {
            var asOf = asOfUtc ?? DateTime.UtcNow;
            var allPreferred = (await _preferredVersionRepository.GetAllAsync().ConfigureAwait(false)).ToList();
            var preferredByCardID = allPreferred
                .GroupBy(pv => pv.CardID)
                .ToDictionary(g => g.Key, g => g.ToList());

            var ownedQuantities = await _collectionRepository.GetOwnedQuantitiesForPreferredVersionsAsync(
                allPreferred.Select(pv => (pv.ImageID, pv.SetCode, pv.RarityName))).ConfigureAwait(false);

            var results = new List<PurchasePriorityCandidateViewModel>();

            // Only cards already on the wishlist (i.e. with at least one preferred version) are considered —
            // this is a prioritization tool for what to buy next, not a discovery tool for new cards to want.
            // If any one of a card's preferred artworks is already fully collected, the whole card is deprioritized.
            foreach (var (cardID, preferredVersions) in preferredByCardID)
            {
                var anyComplete = preferredVersions.Any(pv =>
                    ownedQuantities.GetValueOrDefault((pv.ImageID, pv.SetCode)) >= CardPrinting.CompleteThreshold);
                if (anyComplete)
                    continue;

                var card = _cardDataRepository.GetCardByID(cardID);
                if (card is null)
                    continue;

                foreach (var preferred in preferredVersions)
                {
                    var candidate = PurchasePriorityAnalyzer.Evaluate(
                        card, preferred.SetCode, preferred.RarityName, _cardSetRepository.GetTCGDateBySetCode, asOf);
                    if (candidate is null)
                        continue;

                    var printing = BuildCardPrinting(cardID, preferred.ImageID, preferred.SetCode, preferred.RarityName);
                    if (maxPrice.HasValue && (!printing.Price.HasValue || printing.Price.Value <= 0 || printing.Price.Value > maxPrice.Value))
                        continue; // Price of 0 means no pricing data, not a genuinely free card — exclude, don't let it slip under the cap

                    var quantityOwned = ownedQuantities.GetValueOrDefault((preferred.ImageID, preferred.SetCode));
                    results.Add(PurchasePriorityCandidateViewModel.From(printing, candidate, quantityOwned));
                }
            }

            return results
                .OrderBy(r => r.FoilCount)
                .ThenBy(r => r.PrintingDate, StringComparer.Ordinal)
                .ToList();
        }

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

        public async Task<IReadOnlyDictionary<string, string>> GetTrackedCardImageMapAsync()
        {
            var names = await _collectionEntryValueRepository.GetDistinctCardNamesAsync().ConfigureAwait(false);
            var nameToCard = _cardDataRepository.GetBrowseableCards()
                .Where(c => c.Name is not null && c.CardImages?.Count > 0)
                .GroupBy(c => c.Name!)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            var map = new Dictionary<string, string>();
            foreach (var name in names)
            {
                if (nameToCard.TryGetValue(name, out var card))
                {
                    var url = card.CardImages![0].ImageURLSmall ?? card.CardImages[0].ImageURL;
                    if (url is not null) map[name] = url;
                }
            }
            return map;
        }

        public async Task<IEnumerable<WishlistItemViewModel>> GetWishlistAsync()
        {
            var allPreferred = (await _preferredVersionRepository.GetAllAsync().ConfigureAwait(false)).ToList();
            if (allPreferred.Count == 0)
                return [];

            var ownedQuantities = await _collectionRepository.GetOwnedQuantitiesForPreferredVersionsAsync(
                allPreferred.Select(pv => (pv.ImageID, pv.SetCode, pv.RarityName))).ConfigureAwait(false);

            var results = new List<WishlistItemViewModel>();

            foreach (var pv in allPreferred)
            {
                ownedQuantities.TryGetValue((pv.ImageID, pv.SetCode), out var ownedQty);

                if (ownedQty >= CardPrinting.CompleteThreshold)
                    continue;

                var printing = BuildCardPrinting(pv.CardID, pv.ImageID, pv.SetCode, pv.RarityName);
                results.Add(WishlistItemViewModel.From(printing, ownedQty));
            }

            return results.OrderBy(r => r.CardName).ThenBy(r => r.SetCode);
        }

        public async Task RemoveFromWishlistAsync(int imageID) =>
            await _preferredVersionRepository.DeleteAsync(imageID).ConfigureAwait(false);

        public async Task SavePreferredVersionAsync(int cardID, int imageID, string setCode, string? rarityName = null)
        {
            await _preferredVersionRepository.AddOrUpdateAsync(cardID, imageID, setCode, rarityName).ConfigureAwait(false);
            await AutoDismissNewPrintingsForCardAsync(cardID, setCode).ConfigureAwait(false);
        }

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

            if (criteria.InCollection.HasValue || criteria.IsOrdered.HasValue || criteria.InWishlist.HasValue)
            {
                IReadOnlySet<int>? ownedIDs = null;
                IReadOnlySet<int>? orderedIDs = null;
                IReadOnlySet<int>? preferredIDs = null;

                if (criteria.InCollection.HasValue || criteria.InWishlist.HasValue)
                    ownedIDs = await _collectionRepository.GetCardIDsByStatusAsync(CollectionStatus.Owned).ConfigureAwait(false);

                if (criteria.IsOrdered.HasValue || criteria.InWishlist.HasValue)
                    orderedIDs = await _collectionRepository.GetCardIDsByStatusAsync(CollectionStatus.Ordered).ConfigureAwait(false);

                if (criteria.InWishlist.HasValue)
                    preferredIDs = await _preferredVersionRepository.GetPreferredCardIDsAsync().ConfigureAwait(false);

                if (criteria.InCollection == true)  filtered = filtered.Where(c => ownedIDs!.Contains(c.ID));
                if (criteria.InCollection == false) filtered = filtered.Where(c => !ownedIDs!.Contains(c.ID));
                if (criteria.IsOrdered == true)     filtered = filtered.Where(c => orderedIDs!.Contains(c.ID));
                if (criteria.IsOrdered == false)    filtered = filtered.Where(c => !orderedIDs!.Contains(c.ID));

                if (criteria.InWishlist == true)
                {
                    var collectedIDs = ownedIDs!.Union(orderedIDs!).ToHashSet();
                    filtered = filtered.Where(c => preferredIDs!.Contains(c.ID) && !collectedIDs.Contains(c.ID));
                }
                else if (criteria.InWishlist == false)
                {
                    var collectedIDs = ownedIDs!.Union(orderedIDs!).ToHashSet();
                    filtered = filtered.Where(c => !preferredIDs!.Contains(c.ID) || collectedIDs.Contains(c.ID));
                }
            }

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

        public async Task<PagedResult<CheckedOutCardViewModel>> SearchCheckedOutAsync(CheckedOutSearchCriteria criteria)
        {
            var records = await _checkedOutRepository.GetAllAsync().ConfigureAwait(false);

            var pairs = records.Select(r => (r.ImageID, r.SetCode, r.RarityName)).ToList();
            var ownedQuantities = await _collectionRepository.GetOwnedQuantitiesForPairsAsync(pairs).ConfigureAwait(false);

            var enriched = records
                .Select(r =>
                {
                    var printing = BuildCardPrinting(r.CardID, r.ImageID, r.SetCode, r.RarityName);
                    ownedQuantities.TryGetValue((r.ImageID, r.SetCode, r.RarityName), out var totalOwned);
                    return CheckedOutCardViewModel.From(printing, r.CheckedOutDate, r.Quantity, totalOwned);
                })
                .ToList();

            IEnumerable<CheckedOutCardViewModel> filtered = enriched;

            if (!string.IsNullOrWhiteSpace(criteria.Query))
                filtered = filtered.Where(c => c.CardName.Contains(criteria.Query, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(criteria.CardType))
                filtered = filtered.Where(c => c.CardType.Contains(criteria.CardType, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(criteria.SetName))
            {
                var setPrefix = _cardDataRepository.GetSetPrefixByName(criteria.SetName);
                if (!string.IsNullOrWhiteSpace(setPrefix))
                    filtered = filtered.Where(c => GetSetPrefix(c.SetCode).Equals(setPrefix, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(criteria.RarityName))
                filtered = filtered.Where(c => c.RarityName == criteria.RarityName);

            var filteredList = filtered.OrderBy(c => c.CardName).ThenBy(c => c.SetCode).ToList();
            var totalCount = filteredList.Count;
            var totalQuantitySum = filteredList.Sum(c => c.CheckedOutQuantity);
            var items = filteredList.Skip((criteria.Page - 1) * criteria.PageSize).Take(criteria.PageSize).ToList();

            return new PagedResult<CheckedOutCardViewModel>
            {
                Items = items,
                Page = criteria.Page,
                PageSize = criteria.PageSize,
                TotalCount = totalCount,
                TotalQuantitySum = totalQuantitySum
            };
        }

        public async Task<PagedResult<EditionAuditGroupViewModel>> SearchEditionAuditAsync(EditionAuditSearchCriteria criteria)
        {
            var allGroups = await GetGroupedEditionAuditResultsAsync().ConfigureAwait(false);

            var setPrefix = string.IsNullOrWhiteSpace(criteria.SetName) ? null : _cardDataRepository.GetSetPrefixByName(criteria.SetName);
            var filtered = ApplyCommonPrintingFilters(
                allGroups,
                criteria.Query,
                criteria.CardType,
                setPrefix,
                criteria.RarityName);

            if (criteria.Category.HasValue)
                filtered = filtered.Where(g => g.Entries.Any(e => e.Category == criteria.Category.Value));

            var filteredList = filtered.OrderBy(g => g.CardName).ThenBy(g => g.SetCode).ToList();
            var totalCount = filteredList.Count;
            var items = filteredList.Skip((criteria.Page - 1) * criteria.PageSize).Take(criteria.PageSize).ToList();

            return new PagedResult<EditionAuditGroupViewModel>
            {
                Items = items,
                Page = criteria.Page,
                PageSize = criteria.PageSize,
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

            if (criteria.IsCheckedOut.HasValue)
                filtered = filtered.Where(g => g.IsCheckedOut == criteria.IsCheckedOut.Value);

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

        public async Task UpgradePreferredVersionAsync(int imageID, int cardID, string newSetCode, string newRarityName)
        {
            await _preferredVersionRepository.AddOrUpdateAsync(cardID, imageID, newSetCode, newRarityName).ConfigureAwait(false);
            await AutoDismissNewPrintingsForCardAsync(cardID, newSetCode).ConfigureAwait(false);
        }

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

        private async Task AutoDismissNewPrintingsForCardAsync(int cardID, string setCode)
        {
            var card = _cardDataRepository.GetCardByID(cardID);
            if (card?.CardSets is null)
                return;

            var cutoffDate = _cardSetRepository.GetTCGDateBySetCode(setCode);
            if (cutoffDate is null)
                return;

            var today = DateTime.UtcNow.ToString(SnapshotDateFormat);

            var toDismiss = card.CardSets
                .Select(s => (Set: s, Date: _cardSetRepository.GetTCGDateBySetCode(s.Code ?? string.Empty)))
                .Where(x => x.Date is not null
                            && string.Compare(x.Date, cutoffDate, StringComparison.Ordinal) > 0
                            && string.Compare(x.Date, today, StringComparison.Ordinal) <= 0)
                .ToList();

            foreach (var (set, _) in toDismiss)
                await _dismissedNewPrintingRepository.AddAsync(cardID, set.Code ?? string.Empty, set.RarityName ?? string.Empty)
                    .ConfigureAwait(false);
        }

        private static (EditionAuditCategory? Category, IReadOnlyList<CardEdition> AvailableEditions) CategorizeEdition(
            IReadOnlyDictionary<(string SetCode, string RarityName), IReadOnlySet<CardEdition>> editionMap,
            string setCode,
            string rarityName,
            CardEdition recordedEdition)
        {
            var key = (SetCode: setCode.ToUpperInvariant(), RarityName: rarityName.ToUpperInvariant());

            if (!editionMap.TryGetValue(key, out var availableEditions) || availableEditions.Count == 0)
                return (EditionAuditCategory.Unverifiable, []);

            var orderedEditions = availableEditions.OrderBy(e => e).ToList();

            if (!availableEditions.Contains(recordedEdition))
                return (EditionAuditCategory.EditionMismatch, orderedEditions);

            return (null, orderedEditions);
        }

        private CardPrinting BuildCardPrinting(int cardID, int imageID, string setCode, string? rarityNameHint)
        {
            var card = _cardDataRepository.GetCardByID(cardID);
            var image = card?.CardImages?.FirstOrDefault(i => i.ID == imageID);
            var set = rarityNameHint is not null
                ? card?.CardSets?.FirstOrDefault(s =>
                    string.Equals(s.Code, setCode, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(s.RarityName, rarityNameHint, StringComparison.OrdinalIgnoreCase))
                : card?.CardSets?.FirstOrDefault(s => string.Equals(s.Code, setCode, StringComparison.OrdinalIgnoreCase));
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

        private async Task<IReadOnlyList<EditionAuditResult>> GetEditionAuditResultsAsync()
        {
            var owned = await _collectionRepository.GetByStatusAsync(CollectionStatus.Owned).ConfigureAwait(false);
            var ordered = await _collectionRepository.GetByStatusAsync(CollectionStatus.Ordered).ConfigureAwait(false);

            var entries = owned.Concat(ordered)
                .Where(e => !string.IsNullOrWhiteSpace(e.RarityName) && e.Edition.HasValue)
                .ToList();

            var results = new List<EditionAuditResult>();

            foreach (var group in entries.GroupBy(e => e.CardID))
            {
                var editionMap = await _pricingService.GetCardEditionMapAsync(group.Key).ConfigureAwait(false);

                foreach (var entry in group)
                {
                    var recordedEdition = entry.Edition!.Value;
                    var (category, availableEditions) = CategorizeEdition(editionMap, entry.SetCode, entry.RarityName!, recordedEdition);

                    if (category is not null)
                    {
                        var printing = BuildCardPrinting(entry.CardID, entry.ImageID, entry.SetCode, entry.RarityName);
                        results.Add(EditionAuditResult.From(printing, entry.ID, recordedEdition, availableEditions, category.Value));
                    }
                }
            }

            return results;
        }

        private async Task<IReadOnlyList<EditionAuditGroupViewModel>> GetGroupedEditionAuditResultsAsync()
        {
            var flaggedResults = await GetEditionAuditResultsAsync().ConfigureAwait(false);
            if (flaggedResults.Count == 0)
                return [];

            var flaggedByEntryID = flaggedResults.ToDictionary(r => r.CollectionEntryID);

            var groups = new List<EditionAuditGroupViewModel>();

            foreach (var cardID in flaggedResults.Select(r => r.CardID).Distinct())
            {
                var cardEntries = await _collectionRepository.GetByCardIDAsync(cardID).ConfigureAwait(false);
                var enrichedEntries = cardEntries
                    .Where(e => e.Status == CollectionStatus.Owned || e.Status == CollectionStatus.Ordered)
                    .Select(e =>
                    {
                        var printing = BuildCardPrinting(e.CardID, e.ImageID, e.SetCode, e.RarityName);
                        var baseEntry = OrderEntryViewModel.From(printing, e);
                        var isFlagged = flaggedByEntryID.TryGetValue(e.ID, out var flagged);
                        return EditionAuditEntryViewModel.From(
                            baseEntry,
                            isFlagged ? flagged!.Category : null,
                            isFlagged ? flagged!.AvailableEditions : []);
                    })
                    .ToList();

                foreach (var group in enrichedEntries.GroupBy(e => (e.CardID, e.SetCode, e.RarityCode)))
                {
                    if (!group.Any(e => e.Category.HasValue))
                        continue;

                    groups.Add(EditionAuditGroupViewModel.From(group.First(), group.ToList()));
                }
            }

            return groups;
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
