using CardCollector.Data.Models;
using CardCollector.DTO;
using CardCollector.Extensions;
using CardCollector.Models.Catalog;
using CardCollector.Repository;
using CardCollector.ViewModels;
using Microsoft.Extensions.Logging;

namespace CardCollector.Services
{
    public sealed class CardService : ICardService
    {
        private const int MaxCartQuantity = 3;
        private const int SetBreakdownItemLimit = 20;
        private const string SnapshotDateFormat = "yyyy-MM-dd";
        private readonly ICardDataRepository _cardDataRepository;
        private readonly ICardSetRepository _cardSetRepository;
        private readonly ICheckedOutRepository _checkedOutRepository;
        private readonly ICollectionEntryValueRepository _collectionEntryValueRepository;
        private readonly ICollectionRepository _collectionRepository;
        private readonly ICollectionValueRepository _collectionValueRepository;
        private readonly IDismissedNewPrintingRepository _dismissedNewPrintingRepository;
        private readonly IIgnoredCardRepository _ignoredCardRepository;
        private readonly ILogger<CardService> _logger;
        private readonly IPendingOrderRepository _pendingOrderRepository;
        private readonly IPreferredVersionRepository _preferredVersionRepository;
        private readonly IPricingService _pricingService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IWishlistValueRepository _wishlistValueRepository;

        // Memoized per instance: CardService is Scoped (one instance per request) and no request
        // mutates PreferredVersion/collection state and then re-reads the wishlist within the same request.
        private IReadOnlyList<WishlistItemViewModel>? _wishlistCache;

        public CardService(
            ICardDataRepository cardDataRepository,
            ICardSetRepository cardSetRepository,
            ICheckedOutRepository checkedOutRepository,
            ICollectionRepository collectionRepository,
            ICollectionEntryValueRepository collectionEntryValueRepository,
            ICollectionValueRepository collectionValueRepository,
            IDismissedNewPrintingRepository dismissedNewPrintingRepository,
            IIgnoredCardRepository ignoredCardRepository,
            ILogger<CardService> logger,
            IPendingOrderRepository pendingOrderRepository,
            IPreferredVersionRepository preferredVersionRepository,
            IPricingService pricingService,
            IUnitOfWork unitOfWork,
            IWishlistValueRepository wishlistValueRepository)
        {
            _cardDataRepository = cardDataRepository;
            _cardSetRepository = cardSetRepository;
            _checkedOutRepository = checkedOutRepository;
            _collectionEntryValueRepository = collectionEntryValueRepository;
            _collectionRepository = collectionRepository;
            _collectionValueRepository = collectionValueRepository;
            _dismissedNewPrintingRepository = dismissedNewPrintingRepository;
            _ignoredCardRepository = ignoredCardRepository;
            _logger = logger;
            _pendingOrderRepository = pendingOrderRepository;
            _preferredVersionRepository = preferredVersionRepository;
            _pricingService = pricingService;
            _unitOfWork = unitOfWork;
            _wishlistValueRepository = wishlistValueRepository;
        }

        public async Task AddEntryAsync(
            int printingID, CollectionStatus status,
            int quantity, CardCondition? condition, CardEdition? edition,
            AcquisitionMethod? acquisitionMethod,
            DateTime? purchaseDate, decimal? purchasePrice, decimal? marketPriceAtEntry = null)
        {
            var printing = GetRequiredPrinting(printingID);
            var entry = new CollectionEntry
            {
                AcquisitionMethod = acquisitionMethod,
                CardID = printing.Card.ID,
                CatalogPrintingID = printingID,
                Condition = condition,
                DateCreated = DateTime.UtcNow,
                DateModified = DateTime.UtcNow,
                Edition = edition,
                MarketPriceAtEntry = marketPriceAtEntry,
                PurchaseDate = purchaseDate,
                PurchasePrice = purchasePrice,
                Quantity = quantity < 1 ? 1 : quantity,
                Status = status
            };

            await _collectionRepository.AddAsync(entry).ConfigureAwait(false);
            await AutoDismissNewPrintingsForCardAsync(printing).ConfigureAwait(false);
        }

        public async Task<(int Count, decimal Total, int CartQuantity)> AddToCartAsync(int printingID, int quantity, decimal? marketPrice)
        {
            var printing = GetRequiredPrinting(printingID);
            var line = new PendingOrderLine
            {
                AcquisitionMethod = AcquisitionMethod.Purchased,
                CardID = printing.Card.ID,
                CatalogPrintingID = printingID,
                Condition = CardCondition.NearMint,
                DateCreated = DateTime.UtcNow,
                Edition = CardEdition.FirstEdition,
                MarketPriceAtEntry = marketPrice,
                PurchaseDate = DateTime.UtcNow.Date,
                Quantity = Math.Clamp(quantity, 1, MaxCartQuantity)
            };

            await _pendingOrderRepository.AddRangeAsync([line]).ConfigureAwait(false);

            var (count, total) = await _pendingOrderRepository.GetSummaryAsync().ConfigureAwait(false);
            var stagedQuantities = await _pendingOrderRepository.GetStagedQuantitiesAsync().ConfigureAwait(false);
            return (count, total, stagedQuantities.GetValueOrDefault(printingID));
        }

        public async Task<(decimal TotalValue, int CardCount, IReadOnlyList<(string Label, decimal Value)> SetValueBreakdown, IReadOnlyList<(string CardName, string SetName, string RarityName, decimal Value)> TopValueCards)> CalculateCurrentMarketValueAsync(Func<int, int, Task>? onProgress = null)
        {
            var today = DateTime.UtcNow.ToString(SnapshotDateFormat);

            var latestSnapshot = await _collectionValueRepository.GetLatestSnapshotAsync().ConfigureAwait(false);
            if (latestSnapshot is not null && latestSnapshot.SnapshotDate == today)
            {
                var cachedEntrySnapshots = (await _collectionEntryValueRepository.GetLatestSnapshotsAsync().ConfigureAwait(false)).ToList();
                var cachedEntries = (await _collectionRepository.GetByStatusAsync(CollectionStatus.Owned).ConfigureAwait(false)).ToList();
                return (latestSnapshot.TotalValue, latestSnapshot.CardCount, BuildSetValueBreakdown(cachedEntrySnapshots), BuildTopValueCards(cachedEntrySnapshots, cachedEntries));
            }

            var entries = (await _collectionRepository.GetByStatusAsync(CollectionStatus.Owned).ConfigureAwait(false)).ToList();
            var previousSnapshots = (await _collectionEntryValueRepository.GetLatestSnapshotsAsync().ConfigureAwait(false))
                .ToDictionary(s => s.CollectionEntryID);

            var printingsByEntryID = entries.ToDictionary(
                e => e.ID,
                e => e.CatalogPrintingID is int id ? _cardDataRepository.GetPrintingByID(id) : null);
            var uniquePriceKeys = entries
                .Where(e => printingsByEntryID[e.ID] is not null)
                .Select(e => (PrintingID: e.CatalogPrintingID!.Value, e.Edition))
                .Distinct()
                .ToList();

            var priceCache = new Dictionary<(int PrintingID, CardEdition? Edition), decimal?>();
            var processedPrintings = 0;
            foreach (var key in uniquePriceKeys)
            {
                priceCache[key] = await _pricingService.GetPrintingPriceAsync(key.PrintingID, key.Edition).ConfigureAwait(false);
                if (onProgress is not null)
                    await onProgress(++processedPrintings, uniquePriceKeys.Count).ConfigureAwait(false);
            }

            var setNamesByCode = _cardDataRepository.GetSetNamesByCode();
            var entrySnapshots = new List<CollectionEntryValueSnapshot>();
            decimal totalValue = 0m;

            foreach (var entry in entries)
            {
                var printing = printingsByEntryID[entry.ID];
                previousSnapshots.TryGetValue(entry.ID, out var previous);
                decimal? price = printing is null ? null : priceCache.GetValueOrDefault((entry.CatalogPrintingID!.Value, entry.Edition));
                var label = printing is null ? previous?.SetCode : $"{printing.Printing.Code} {printing.Printing.RarityName}";

                decimal entryValue;
                if (price.HasValue)
                {
                    entryValue = price.Value * entry.Quantity;
                }
                else if (previous is not null)
                {
                    entryValue = previous.MarketValue;
                    _logger.LogWarning("Price unavailable for entry {EntryID} ({Printing}); using previous snapshot value {Value}", entry.ID, label, entryValue);
                }
                else if (entry.MarketPriceAtEntry.HasValue)
                {
                    entryValue = entry.MarketPriceAtEntry.Value * entry.Quantity;
                    _logger.LogWarning("Price unavailable for entry {EntryID} ({Printing}) and no previous snapshot exists; falling back to market price at time of purchase {Value}",
                        entry.ID, label, entryValue);
                }
                else
                {
                    continue;
                }

                totalValue += entryValue;

                // A printing that has left the catalog keeps the labels its last snapshot had.
                var setCode = printing?.Printing.Code ?? previous?.SetCode ?? string.Empty;
                entrySnapshots.Add(new CollectionEntryValueSnapshot
                {
                    CardName = printing?.Card.Name ?? previous?.CardName ?? string.Empty,
                    CollectionEntryID = entry.ID,
                    DateCreated = DateTime.UtcNow,
                    Edition = entry.Edition,
                    MarketValue = entryValue,
                    RarityName = RarityExtensions.NormalizeRarityName(printing?.Printing.RarityName) ?? previous?.RarityName ?? string.Empty,
                    SetCode = setCode,
                    SetName = setNamesByCode.TryGetValue(setCode, out var setName) ? setName : previous?.SetName ?? setCode,
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

            return (totalValue, entries.Sum(e => e.Quantity), BuildSetValueBreakdown(entrySnapshots), BuildTopValueCards(entrySnapshots, entries));
        }

        public async Task<(decimal TotalValue, int CountRemaining)> CalculateWishlistRemainingValueAsync()
        {
            var today = DateTime.UtcNow.ToString(SnapshotDateFormat);

            var latestSnapshot = await _wishlistValueRepository.GetLatestSnapshotAsync().ConfigureAwait(false);
            if (latestSnapshot is not null && latestSnapshot.SnapshotDate == today)
                return (latestSnapshot.TotalValue, latestSnapshot.RemainingCount);

            var wishlist = (await GetWishlistAsync().ConfigureAwait(false)).ToList();

            var totalValue = 0m;
            var countRemaining = 0;
            foreach (var item in wishlist)
            {
                var price = item.PrintingID > 0 ? await _pricingService.GetPrintingPriceAsync(item.PrintingID).ConfigureAwait(false) : null;
                totalValue += (price ?? 0m) * item.QuantityNeeded;
                countRemaining += item.QuantityNeeded;
            }

            await _wishlistValueRepository.UpsertSnapshotAsync(new WishlistValueSnapshot
            {
                DateCreated = DateTime.UtcNow,
                RemainingCount = countRemaining,
                SnapshotDate = today,
                TotalValue = totalValue
            }).ConfigureAwait(false);

            return (totalValue, countRemaining);
        }

        public async Task<EditionAuditCategory?> CheckEntryEditionAsync(int printingID, CardEdition edition)
        {
            var printing = _cardDataRepository.GetPrintingByID(printingID);
            if (printing is null)
                return EditionAuditCategory.Unverifiable;

            var editions = await _pricingService.GetEditionsByPrintingAsync(printing.Card.ID).ConfigureAwait(false);
            return CategorizeEdition(editions, printingID, edition).Category;
        }

        public async Task CheckInCardAsync(int printingID) =>
            await _checkedOutRepository.RemoveAsync(printingID).ConfigureAwait(false);

        public async Task CheckOutCardAsync(int printingID, int quantity)
        {
            var existing = await _checkedOutRepository.GetAsync(printingID).ConfigureAwait(false);
            if (existing is not null)
            {
                await _checkedOutRepository.UpdateAsync(printingID, quantity).ConfigureAwait(false);
                return;
            }

            var printing = GetRequiredPrinting(printingID);
            var now = DateTime.UtcNow;
            await _checkedOutRepository.AddAsync(new CheckedOutCard
            {
                CardID = printing.Card.ID,
                CatalogPrintingID = printingID,
                CheckedOutDate = now,
                DateCreated = now,
                DateModified = now,
                Quantity = quantity
            }).ConfigureAwait(false);
        }

        public async Task DismissNewPrintingAsync(int printingID)
        {
            var printing = GetRequiredPrinting(printingID);
            await _dismissedNewPrintingRepository.AddAsync(printingID, printing.Card.ID).ConfigureAwait(false);
        }

        public Card? GetCardByID(int cardID) => _cardDataRepository.GetCardByID(cardID);

        public CatalogPrintingRef? GetPrintingByID(int printingID) => _cardDataRepository.GetPrintingByID(printingID);

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

        public async Task<(int Count, decimal Total)> GetCartSummaryAsync() =>
            await _pendingOrderRepository.GetSummaryAsync().ConfigureAwait(false);

        public async Task<CollectionStatsViewModel> GetCollectionStatsAsync()
        {
            var ownedEntries = (await _collectionRepository.GetByStatusAsync(CollectionStatus.Owned).ConfigureAwait(false)).ToList();
            var setNamesByCode = _cardDataRepository.GetSetNamesByCode();
            var printings = ownedEntries
                .Select(e => (Entry: e, Printing: e.CatalogPrintingID is int id ? _cardDataRepository.GetPrintingByID(id) : null))
                .ToList();

            var rarityBreakdown = printings
                .GroupBy(x => RarityExtensions.NormalizeRarityName(x.Printing?.Printing.RarityName) ?? "Unknown")
                .Select(g => (g.Key, g.Count()))
                .OrderByDescending(x => x.Item2)
                .Take(10)
                .ToList();

            var setBreakdown = printings
                .Where(x => x.Printing is not null)
                .Select(x => (CardID: x.Printing!.Card.ID, SetCode: x.Printing.Printing.Code ?? string.Empty))
                .GroupBy(x => setNamesByCode.TryGetValue(x.SetCode, out var name) ? name : x.SetCode)
                .Select(g => (g.Key, g.Distinct().Count()))
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
            var valueHistory = await _collectionValueRepository.GetAllSnapshotsAsync().ConfigureAwait(false);
            var wishlistValueHistory = await _wishlistValueRepository.GetAllSnapshotsAsync().ConfigureAwait(false);

            return new CollectionStatsViewModel
            {
                AcquisitionBreakdown = acquisitionBreakdown,
                RarityBreakdown = rarityBreakdown,
                SetBreakdown = setBreakdown,
                SetValueBreakdown = BuildSetValueBreakdown(latestEntrySnapshots),
                TopValueCards = BuildTopValueCards(latestEntrySnapshots, ownedEntries),
                ValueHistory = MapValueHistory(valueHistory),
                WishlistValueHistory = MapValueHistory(wishlistValueHistory)
            };
        }

        public async Task<DashboardStats> GetDashboardStatsAsync()
        {
            var ignoredCardIDs = await _ignoredCardRepository.GetIgnoredCardIDsAsync().ConfigureAwait(false);
            var totalCards = _cardDataRepository.GetBrowseableCards().Count(c => !ignoredCardIDs.Contains(c.ID));
            var groups = (await GetGroupedOwnedAsync().ConfigureAwait(false)).Where(g => !ignoredCardIDs.Contains(g.CardID));
            var ordered = await _collectionRepository.GetByStatusAsync(CollectionStatus.Ordered).ConfigureAwait(false);
            var ownedStats = await _collectionRepository.GetOwnedStatsAsync().ConfigureAwait(false);
            var latestSnapshot = await _collectionValueRepository.GetLatestSnapshotAsync().ConfigureAwait(false);
            var latestWishlistSnapshot = await _wishlistValueRepository.GetLatestSnapshotAsync().ConfigureAwait(false);

            var ownedPrintingIDs = await _collectionRepository.GetOwnedPrintingIDsAsync().ConfigureAwait(false);
            var allPreferred = await _preferredVersionRepository.GetAllAsync().ConfigureAwait(false);
            var wishlistCount = allPreferred.Count(pv => pv.CatalogPrintingID is not int id || !ownedPrintingIDs.Contains(id));

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
                WishlistCount = wishlistCount,
                WishlistRemainingValue = latestWishlistSnapshot?.TotalValue,
                WishlistRemainingValueDate = latestWishlistSnapshot?.SnapshotDate
            };
        }

        public async Task<IEnumerable<EditionAuditEntryViewModel>> GetEnrichedOrdersAsync()
        {
            var entries = await _collectionRepository.GetByStatusAsync(CollectionStatus.Ordered).ConfigureAwait(false);
            var viewModels = new List<EditionAuditEntryViewModel>();

            foreach (var group in entries.GroupBy(e => e.CardID))
            {
                var editions = await _pricingService.GetEditionsByPrintingAsync(group.Key).ConfigureAwait(false);

                foreach (var entry in group)
                {
                    var baseEntry = OrderEntryViewModel.From(BuildCardPrinting(entry), entry);
                    var (category, availableEditions) = entry.Edition is CardEdition recorded
                        ? CategorizeEdition(editions, entry.CatalogPrintingID, recorded)
                        : (null, []);
                    viewModels.Add(EditionAuditEntryViewModel.From(baseEntry, category, availableEditions));
                }
            }

            return viewModels;
        }

        public Task<IEnumerable<OrderEntryViewModel>> GetEnrichedOwnedAsync()
            => GetEnrichedByStatusAsync(CollectionStatus.Owned);

        public async Task<IEnumerable<CollectionEntry>> GetEntriesByCardIDAsync(int cardID) =>
            await _collectionRepository.GetByCardIDAsync(cardID).ConfigureAwait(false);

        public async Task<IEnumerable<CollectionGroupViewModel>> GetGroupedOwnedAsync()
        {
            var entries = (await GetEnrichedByStatusAsync(CollectionStatus.Owned).ConfigureAwait(false)).ToList();

            var cardIDs = entries.Select(e => e.CardID).Distinct().ToHashSet();
            var preferredVersions = await _preferredVersionRepository.GetByCardIDsAsync(cardIDs).ConfigureAwait(false);
            var checkedOutLookup = await _checkedOutRepository.GetCheckedOutLookupAsync().ConfigureAwait(false);

            // Phase 1: build all groups; non-preferred groups default PreferredVersionIsComplete = false
            var allGroups = entries
                .GroupBy(e => e.PrintingID > 0 ? $"printing:{e.PrintingID}" : $"entry:{e.EntryID}")
                .Select(g =>
                {
                    var first = g.First();
                    var withPrice = g.Where(e => e.PurchasePrice.HasValue).ToList();
                    decimal? totalCost = withPrice.Any()
                        ? withPrice.Sum(e => e.Quantity * e.PurchasePrice!.Value)
                        : null;

                    var matchedPreferred = (preferredVersions.TryGetValue(first.CardID, out var list) ? list : (IReadOnlyList<PreferredVersion>)[])
                        .FirstOrDefault(pv => first.PrintingID > 0 && pv.CatalogPrintingID == first.PrintingID);

                    var hasCheckout = checkedOutLookup.TryGetValue(first.PrintingID, out var checkoutInfo);

                    return CollectionGroupViewModel.From(
                        printing: first,
                        entries: g.ToList(),
                        isPreferredVersion: matchedPreferred is not null,
                        preferredVersionIsComplete: false,
                        totalCost: totalCost,
                        totalQuantity: g.Sum(e => e.Quantity),
                        checkedOutQuantity: hasCheckout ? checkoutInfo.Quantity : 0,
                        checkedOutDate: hasCheckout ? checkoutInfo.Date : null,
                        completeThreshold: matchedPreferred?.DesiredQuantity ?? 3);
                })
                .ToList();

            // Phase 2: identify card IDs where the preferred version group is Complete
            var completePreferredCardIDs = allGroups
                .Where(g => g.IsPreferredVersion && g.TotalQuantity >= g.CompleteThreshold)
                .Select(g => g.CardID)
                .ToHashSet();

            // Phase 3: rebuild non-preferred groups with the correct PreferredVersionIsComplete flag
            return allGroups
                .Select(g => g.IsPreferredVersion ? g : CollectionGroupViewModel.From(
                    printing: g,
                    entries: g.Entries,
                    isPreferredVersion: false,
                    preferredVersionIsComplete: completePreferredCardIDs.Contains(g.CardID),
                    totalCost: g.TotalCost,
                    totalQuantity: g.TotalQuantity,
                    checkedOutQuantity: g.CheckedOutQuantity,
                    checkedOutDate: g.CheckedOutDate,
                    completeThreshold: g.CompleteThreshold))
                .OrderBy(g => g.CardName)
                .ThenBy(g => g.SetCode)
                .ToList();
        }

        public async Task<IReadOnlyList<NewPrintingOpportunityViewModel>> GetNewPrintingOpportunitiesAsync()
        {
            var preferredVersions = (await _preferredVersionRepository.GetAllAsync().ConfigureAwait(false)).ToList();
            var dismissed = await _dismissedNewPrintingRepository.GetAllAsync().ConfigureAwait(false);
            var ignoredCards = await _ignoredCardRepository.GetAllAsync().ConfigureAwait(false);
            var setNamesByCode = _cardDataRepository.GetSetNamesByCode();
            var result = new List<NewPrintingOpportunityViewModel>();
            var today = DateTime.UtcNow.ToString(SnapshotDateFormat);

            foreach (var pv in preferredVersions)
            {
                if (pv.CatalogPrintingID is not int trackedID || _cardDataRepository.GetPrintingByID(trackedID) is not { } tracked)
                    continue;

                var card = tracked.Card;
                var trackedSetCode = tracked.Printing.Code ?? string.Empty;
                var preferredDate = _cardSetRepository.GetTCGDateBySetCode(trackedSetCode);
                if (preferredDate is null)
                    continue;

                var newerPrintings = BuildNewerPrintings(card, preferredDate, today, dismissed, setNamesByCode, exceptPrintingID: trackedID);
                if (newerPrintings.Count == 0)
                    continue;

                result.Add(new NewPrintingOpportunityViewModel
                {
                    CardID = card.ID,
                    CardName = card.Name ?? string.Empty,
                    CurrentPrintVariant = tracked.Printing.PrintVariant,
                    CurrentRarityName = RarityExtensions.NormalizeRarityName(tracked.Printing.RarityName) ?? string.Empty,
                    CurrentReleaseDate = preferredDate,
                    CurrentSetCode = trackedSetCode,
                    CurrentSetName = setNamesByCode.TryGetValue(trackedSetCode, out var currentSetName) ? currentSetName : trackedSetCode,
                    ImageURLSmall = card.CardImages?.FirstOrDefault()?.ImageURLSmall ?? string.Empty,
                    IsIgnored = ignoredCards.ContainsKey(card.ID),
                    NewerPrintings = newerPrintings,
                    PreferredVersionID = pv.ID
                });
            }

            // Ignored cards with no preferred version at all have no set to compare against, so the date
            // they were ignored on stands in as the baseline for "is this printing new."
            var preferredCardIDs = preferredVersions.Select(pv => pv.CardID).ToHashSet();
            foreach (var (cardID, ignoredSince) in ignoredCards.Where(kvp => !preferredCardIDs.Contains(kvp.Key)))
            {
                var card = _cardDataRepository.GetCardByID(cardID);
                if (card?.CardSets is null)
                    continue;

                var newerPrintings = BuildNewerPrintings(card, ignoredSince.ToString(SnapshotDateFormat), today, dismissed, setNamesByCode, exceptPrintingID: null);
                if (newerPrintings.Count == 0)
                    continue;

                result.Add(new NewPrintingOpportunityViewModel
                {
                    CardID = cardID,
                    CardName = card.Name ?? string.Empty,
                    CurrentRarityName = string.Empty,
                    CurrentReleaseDate = null,
                    CurrentSetCode = string.Empty,
                    CurrentSetName = "Not yet tracked",
                    ImageURLSmall = card.CardImages?.FirstOrDefault()?.ImageURLSmall ?? string.Empty,
                    IsIgnored = true,
                    NewerPrintings = newerPrintings
                });
            }

            return result.OrderBy(r => r.CardName).ToList();
        }

        public async Task<(IReadOnlyList<string> SetCodes, IReadOnlyList<string> RarityNames)> GetOwnedSetCodesAndRarityNamesAsync()
        {
            var printings = (await _collectionRepository.GetOwnedPrintingIDsAsync().ConfigureAwait(false))
                .Select(_cardDataRepository.GetPrintingByID)
                .OfType<CatalogPrintingRef>()
                .ToList();

            return (
                printings.Select(p => p.Printing.Code).OfType<string>().Distinct().OrderBy(c => c).ToList(),
                printings.Select(p => RarityExtensions.NormalizeRarityName(p.Printing.RarityName)).OfType<string>().Distinct().OrderBy(r => r).ToList());
        }

        public async Task<IReadOnlyList<PendingOrderLineViewModel>> GetPendingCartAsync()
        {
            var lines = await _pendingOrderRepository.GetAllAsync().ConfigureAwait(false);
            return lines.Select(line => PendingOrderLineViewModel.From(BuildCardPrinting(line), line)).ToList();
        }

        public async Task<PurchasePlanViewModel> GetPurchasePlanAsync(decimal? totalBudget = null, int? maxCards = null, decimal? maxPricePerCard = null, DateTime? asOfUTC = null)
        {
            var candidates = await GetPurchasePriorityCandidatesAsync(asOfUTC, maxPricePerCard).ConfigureAwait(false);

            var items = new List<PurchasePriorityCandidateViewModel>();
            var remainingBudget = totalBudget;

            foreach (var candidate in candidates)
            {
                if (maxCards.HasValue && items.Count >= maxCards.Value)
                    break;

                // Skip, don't stop — a cheaper candidate further down may still fit the budget.
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


        public async Task<PurchasePriorityCandidateViewModel?> GetPurchasePriorityCandidateAsync(
            int printingID, decimal? maxPrice = null, DateTime? asOfUTC = null)
        {
            var asOf = asOfUTC ?? DateTime.UtcNow;

            var printing = _cardDataRepository.GetPrintingByID(printingID);
            if (printing is null)
                return null;

            // A card can have several tracked printings — use this printing's own target, defaulting to 3 if it
            // isn't actually tracked (a candidate can still be evaluated ad hoc).
            var trackedPrintings = await _preferredVersionRepository.GetByCardIDAsync(printing.Card.ID).ConfigureAwait(false);
            var matched = trackedPrintings.FirstOrDefault(pv => pv.CatalogPrintingID == printingID);

            var ownedQuantities = await _collectionRepository.GetOwnedQuantitiesByPrintingAsync([printingID]).ConfigureAwait(false);
            var orderedQuantities = await _collectionRepository.GetOrderedQuantitiesAsync().ConfigureAwait(false);
            var stagedQuantities = await _pendingOrderRepository.GetStagedQuantitiesAsync().ConfigureAwait(false);

            return await EvaluateCandidateAsync(
                printing, asOf, maxPrice,
                ownedQuantities, orderedQuantities, stagedQuantities, matched?.DesiredQuantity ?? 3).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<PurchasePriorityCandidateViewModel>> GetPurchasePriorityCandidatesAsync(DateTime? asOfUTC = null, decimal? maxPrice = null)
        {
            var asOf = asOfUTC ?? DateTime.UtcNow;
            var tracked = (await _preferredVersionRepository.GetAllAsync().ConfigureAwait(false))
                .Where(pv => pv.CatalogPrintingID is not null)
                .ToList();

            var ownedQuantities = await _collectionRepository
                .GetOwnedQuantitiesByPrintingAsync(tracked.Select(pv => pv.CatalogPrintingID!.Value))
                .ConfigureAwait(false);
            var orderedQuantities = await _collectionRepository.GetOrderedQuantitiesAsync().ConfigureAwait(false);
            var stagedQuantities = await _pendingOrderRepository.GetStagedQuantitiesAsync().ConfigureAwait(false);

            var results = new List<PurchasePriorityCandidateViewModel>();

            // Every tracked printing appears here independently; flagged printings (scarce, no reprint in
            // years) sort first and everything else fills out the budget after. Each printing is evaluated
            // on its own target — EvaluateCandidateAsync already excludes one whose own target is already met.
            foreach (var pv in tracked)
            {
                var printing = _cardDataRepository.GetPrintingByID(pv.CatalogPrintingID!.Value);
                if (printing is null)
                    continue;

                var candidateViewModel = await EvaluateCandidateAsync(
                    printing, asOf, maxPrice,
                    ownedQuantities, orderedQuantities, stagedQuantities, pv.DesiredQuantity).ConfigureAwait(false);

                if (candidateViewModel is not null)
                    results.Add(candidateViewModel);
            }

            return results
                .OrderBy(r => r.FoilCount)
                .ThenBy(r => r.PrintingDate, StringComparer.Ordinal)
                .ThenBy(r => r.CardName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public async Task<Card?> GetRandomUncollectedAsync()
        {
            var preferredCardIDs = await _preferredVersionRepository.GetPreferredCardIDsAsync().ConfigureAwait(false);
            var ignoredCardIDs = await _ignoredCardRepository.GetIgnoredCardIDsAsync().ConfigureAwait(false);

            // An ignored card with an unresolved new-printing opportunity gets a chance to resurface here
            // rather than staying excluded forever.
            var opportunities = await GetNewPrintingOpportunitiesAsync().ConfigureAwait(false);
            var resurfacedIgnoredCardIDs = opportunities.Where(o => o.IsIgnored).Select(o => o.CardID).ToHashSet();

            var excludedCardIDs = preferredCardIDs
                .Union(ignoredCardIDs.Except(resurfacedIgnoredCardIDs))
                .ToHashSet();

            var uncollected = _cardDataRepository.GetBrowseableCards()
                .Where(c => !excludedCardIDs.Contains(c.ID))
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

        public async Task<IReadOnlyList<PreferredVersion>> GetTrackedPrintingsByCardIDAsync(int cardID) =>
            await _preferredVersionRepository.GetByCardIDAsync(cardID).ConfigureAwait(false);

        public async Task<IEnumerable<WishlistItemViewModel>> GetWishlistAsync()
        {
            if (_wishlistCache is not null)
                return _wishlistCache;

            var allPreferred = (await _preferredVersionRepository.GetAllAsync().ConfigureAwait(false)).ToList();
            if (allPreferred.Count == 0)
            {
                _wishlistCache = [];
                return _wishlistCache;
            }

            var ownedQuantities = await _collectionRepository
                .GetOwnedQuantitiesByPrintingAsync(allPreferred.Where(pv => pv.CatalogPrintingID is not null).Select(pv => pv.CatalogPrintingID!.Value))
                .ConfigureAwait(false);
            var orderedQuantities = await _collectionRepository.GetOrderedQuantitiesAsync().ConfigureAwait(false);
            var stagedQuantities = await _pendingOrderRepository.GetStagedQuantitiesAsync().ConfigureAwait(false);

            var results = new List<WishlistItemViewModel>();

            foreach (var pv in allPreferred)
            {
                var printingID = pv.CatalogPrintingID ?? 0;
                var ownedQty = ownedQuantities.GetValueOrDefault(printingID);
                if (ownedQty >= pv.DesiredQuantity)
                    continue;

                results.Add(WishlistItemViewModel.From(
                    BuildCardPrinting(pv),
                    pv.ID,
                    ownedQty,
                    stagedQuantities.GetValueOrDefault(printingID),
                    orderedQuantities.GetValueOrDefault(printingID),
                    pv.DesiredQuantity));
            }

            _wishlistCache = results.OrderBy(r => r.CardName).ThenBy(r => r.SetCode).ToList();
            return _wishlistCache;
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

        public async Task IgnoreCardAsync(int cardID) =>
            await _ignoredCardRepository.AddAsync(cardID).ConfigureAwait(false);

        public async Task<bool> IsCardIgnoredAsync(int cardID) =>
            await _ignoredCardRepository.IsIgnoredAsync(cardID).ConfigureAwait(false);

        public async Task RemoveFromWishlistAsync(int preferredVersionID) =>
            await _preferredVersionRepository.DeleteAsync(preferredVersionID).ConfigureAwait(false);

        public async Task SavePreferredVersionAsync(int printingID, int? desiredQuantity = null)
        {
            var printing = GetRequiredPrinting(printingID);
            await _preferredVersionRepository.AddOrUpdateAsync(printingID, printing.Card.ID, desiredQuantity).ConfigureAwait(false);
            await _ignoredCardRepository.RemoveAsync(printing.Card.ID).ConfigureAwait(false);
            await AutoDismissNewPrintingsForCardAsync(printing).ConfigureAwait(false);
        }

        public async Task<PagedResult<CardListItemViewModel>> SearchCardsAsync(BrowseSearchCriteria criteria)
        {
            var page = criteria.Page;
            var pageSize = criteria.PageSize;

            var setPrefix = string.IsNullOrWhiteSpace(criteria.SetName) ? null : _cardDataRepository.GetSetPrefixByName(criteria.SetName);
            var isPrintingScoped = !string.IsNullOrWhiteSpace(setPrefix) || !string.IsNullOrWhiteSpace(criteria.RarityName);
            var filtered = ApplyCommonCardFilters(
                _cardDataRepository.GetBrowseableCards(),
                criteria.Query,
                criteria.CardType,
                setPrefix,
                criteria.RarityName);

            if (criteria.InCollection.HasValue || criteria.IsOrdered.HasValue || criteria.InWishlist.HasValue || criteria.IsTracked.HasValue)
            {
                if (isPrintingScoped)
                {
                    var candidates = filtered.ToList();
                    var needOwned = criteria.InCollection.HasValue || criteria.InWishlist.HasValue;
                    var needOrdered = criteria.IsOrdered.HasValue || criteria.InWishlist.HasValue;
                    var needTracked = criteria.InWishlist.HasValue || criteria.IsTracked.HasValue;

                    var scoped = await GetPrintingScopedDataAsync(
                        candidates.Select(c => c.ID).ToList(), setPrefix, criteria.RarityName, needOwned, needOrdered, needTracked)
                        .ConfigureAwait(false);

                    if (criteria.InCollection == true)  candidates = candidates.Where(c => scoped.OwnedQuantities.GetValueOrDefault(c.ID) > 0).ToList();
                    if (criteria.InCollection == false) candidates = candidates.Where(c => scoped.OwnedQuantities.GetValueOrDefault(c.ID) == 0).ToList();
                    if (criteria.IsOrdered == true)     candidates = candidates.Where(c => scoped.OrderedQuantities.GetValueOrDefault(c.ID) > 0).ToList();
                    if (criteria.IsOrdered == false)    candidates = candidates.Where(c => scoped.OrderedQuantities.GetValueOrDefault(c.ID) == 0).ToList();

                    if (criteria.InWishlist == true)
                        candidates = candidates.Where(c => scoped.TrackedCardIDs.Contains(c.ID)
                            && scoped.OwnedQuantities.GetValueOrDefault(c.ID) == 0
                            && scoped.OrderedQuantities.GetValueOrDefault(c.ID) == 0).ToList();
                    else if (criteria.InWishlist == false)
                        candidates = candidates.Where(c => !scoped.TrackedCardIDs.Contains(c.ID)
                            || scoped.OwnedQuantities.GetValueOrDefault(c.ID) > 0
                            || scoped.OrderedQuantities.GetValueOrDefault(c.ID) > 0).ToList();

                    if (criteria.IsTracked == true)  candidates = candidates.Where(c => scoped.TrackedCardIDs.Contains(c.ID)).ToList();
                    if (criteria.IsTracked == false) candidates = candidates.Where(c => !scoped.TrackedCardIDs.Contains(c.ID)).ToList();

                    filtered = candidates;
                }
                else
                {
                    IReadOnlySet<int>? ownedIDs = null;
                    IReadOnlySet<int>? orderedIDs = null;
                    IReadOnlySet<int>? preferredIDs = null;

                    if (criteria.InCollection.HasValue || criteria.InWishlist.HasValue)
                        ownedIDs = await _collectionRepository.GetCardIDsByStatusAsync(CollectionStatus.Owned).ConfigureAwait(false);

                    if (criteria.IsOrdered.HasValue || criteria.InWishlist.HasValue)
                        orderedIDs = await _collectionRepository.GetCardIDsByStatusAsync(CollectionStatus.Ordered).ConfigureAwait(false);

                    if (criteria.InWishlist.HasValue || criteria.IsTracked.HasValue)
                        preferredIDs = await _preferredVersionRepository.GetPreferredCardIDsAsync().ConfigureAwait(false);

                    IReadOnlySet<int>? untrackedOwnedPrintingCardIDs = criteria.IsTracked.HasValue
                        ? await GetCardIDsWithUntrackedOwnedPrintingsAsync().ConfigureAwait(false)
                        : null;

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

                    // "Tracked" means every owned printing of the card is matched by a tracked version --
                    // not just that the card has a tracked printing somewhere. A card can be owned as one
                    // printing and tracked as a different one; that's still an untracked printing to fix.
                    if (criteria.IsTracked == true)
                        filtered = filtered.Where(c => preferredIDs!.Contains(c.ID) && !untrackedOwnedPrintingCardIDs!.Contains(c.ID));
                    if (criteria.IsTracked == false)
                        filtered = filtered.Where(c => !preferredIDs!.Contains(c.ID) || untrackedOwnedPrintingCardIDs!.Contains(c.ID));
                }
            }

            if (criteria.IsIncomplete == true)
            {
                var candidates = filtered.ToList();

                // When a set and/or rarity filter is active, "incomplete" must mean incomplete *within that
                // printing* — not incomplete on whichever printing the card's tracked version happens to point
                // to. Falls back to the card-wide tracked-version rollup when no set/rarity filter is selected.
                if (isPrintingScoped)
                {
                    var scoped = await GetPrintingScopedDataAsync(
                        candidates.Select(c => c.ID).ToList(), setPrefix, criteria.RarityName, needOwned: true, needOrdered: false, needTracked: true)
                        .ConfigureAwait(false);

                    filtered = candidates.Where(c =>
                        scoped.OwnedQuantities.TryGetValue(c.ID, out var qty) && qty > 0
                        && qty < scoped.MaxDesiredQuantities.GetValueOrDefault(c.ID, 3));
                }
                else
                {
                    var candidateCompletionMap = await BuildCardCompletionMapAsync(candidates).ConfigureAwait(false);
                    filtered = candidates.Where(c => candidateCompletionMap.TryGetValue(c.ID, out var s) && s == CollectionCompletionStatus.Incomplete);
                }
            }

            var orderedFiltered = filtered.OrderBy(c => c.Name).ToList();

            var totalCount = orderedFiltered.Count;
            var slice = orderedFiltered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            var cardIDs = slice.Select(c => c.ID).ToList();
            var statusMap = await _collectionRepository.GetStatusByCardIDsAsync(cardIDs).ConfigureAwait(false);

            // Roll up completion status from all artworks of each card in the slice.
            var cardCompletionMap = await BuildCardCompletionMapAsync(slice).ConfigureAwait(false);

            var items = slice.Select(c => new CardListItemViewModel
            {
                Attribute = c.Attribute ?? string.Empty,
                CardID = c.ID,
                CardType = c.CardType ?? string.Empty,
                CompletionStatus = statusMap.TryGetValue(c.ID, out var rawStatus) && rawStatus == CollectionStatus.Owned
                    ? (cardCompletionMap.TryGetValue(c.ID, out var cs) ? cs : null)
                    : null,
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

            var ownedQuantities = await _collectionRepository
                .GetOwnedQuantitiesByPrintingAsync(records.Where(r => r.CatalogPrintingID is not null).Select(r => r.CatalogPrintingID!.Value))
                .ConfigureAwait(false);

            var enriched = records
                .Select(r =>
                {
                    var printing = BuildCardPrinting(r);
                    var totalOwned = r.CatalogPrintingID is int id ? ownedQuantities.GetValueOrDefault(id) : 0;
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
                filtered = filtered.Where(c => c.RarityName.Equals(criteria.RarityName, StringComparison.OrdinalIgnoreCase));

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

        public async Task<bool> SetDesiredQuantityAsync(int preferredVersionID, int desiredQuantity) =>
            await _preferredVersionRepository.UpdateDesiredQuantityAsync(preferredVersionID, Math.Clamp(desiredQuantity, 1, 99)).ConfigureAwait(false);

        public async Task<(int Count, decimal Total, IReadOnlyList<string> EditionWarnings)> SubmitCartAsync(IReadOnlyList<CartLineOverride> overrides)
        {
            ArgumentNullException.ThrowIfNull(overrides);

            var overridesByLineID = overrides.ToDictionary(o => o.PendingOrderLineID);
            var lines = await _pendingOrderRepository.GetByIDsAsync(overridesByLineID.Keys).ConfigureAwait(false);

            var total = 0m;
            var editionWarnings = new List<string>();
            var submitted = new List<int>();

            // Wrapped in a transaction so a failure partway through (e.g. line 3 of 5 throws) rolls back
            // every entry created in this submission instead of leaving duplicates to resubmit on retry.
            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                foreach (var line in lines)
                {
                    // A line whose printing has left the catalog can't become an entry; it stays in the cart.
                    if (line.CatalogPrintingID is not int printingID || _cardDataRepository.GetPrintingByID(printingID) is not { } printing)
                    {
                        editionWarnings.Add("One cart line's printing is no longer in the catalog, so it was left in the cart.");
                        continue;
                    }

                    var condition = line.Condition;
                    var edition = line.Edition;
                    var purchaseDate = line.PurchaseDate;
                    var purchasePrice = line.PurchasePrice;
                    var marketPriceAtEntry = line.MarketPriceAtEntry;
                    var quantity = line.Quantity;

                    if (overridesByLineID.TryGetValue(line.ID, out var lineOverride))
                    {
                        condition = lineOverride.Condition;
                        edition = lineOverride.Edition;
                        purchaseDate = lineOverride.PurchaseDate;
                        purchasePrice = lineOverride.PurchasePrice;
                        marketPriceAtEntry = lineOverride.MarketPriceAtEntry;
                        quantity = Math.Clamp(lineOverride.Quantity, 1, MaxCartQuantity);
                    }

                    if (edition.HasValue && await CheckEntryEditionAsync(printingID, edition.Value).ConfigureAwait(false) == EditionAuditCategory.EditionMismatch)
                        editionWarnings.Add(EditionWarningExtensions.BuildEditionMismatchMessage(
                            edition.Value, printing.Printing.Code ?? string.Empty, RarityExtensions.NormalizeRarityName(printing.Printing.RarityName) ?? string.Empty));

                    await AddEntryAsync(
                        printingID, CollectionStatus.Ordered,
                        quantity, condition, edition, line.AcquisitionMethod,
                        purchaseDate, purchasePrice, marketPriceAtEntry).ConfigureAwait(false);
                    total += (purchasePrice ?? 0) * quantity;
                    submitted.Add(line.ID);
                }

                await _pendingOrderRepository.DeleteRangeAsync(submitted).ConfigureAwait(false);
            }).ConfigureAwait(false);

            return (submitted.Count, total, editionWarnings);
        }
        public async Task UnignoreCardAsync(int cardID) =>
            await _ignoredCardRepository.RemoveAsync(cardID).ConfigureAwait(false);

        public async Task<bool> UpdateCartLineQuantityAsync(int pendingOrderLineID, int quantity) =>
            await _pendingOrderRepository.UpdateQuantityAsync(pendingOrderLineID, Math.Clamp(quantity, 1, MaxCartQuantity)).ConfigureAwait(false);

        public async Task UpgradePreferredVersionAsync(int preferredVersionID, int newPrintingID)
        {
            var printing = GetRequiredPrinting(newPrintingID);
            await _preferredVersionRepository.UpgradeAsync(preferredVersionID, newPrintingID).ConfigureAwait(false);
            await _ignoredCardRepository.RemoveAsync(printing.Card.ID).ConfigureAwait(false);
            await AutoDismissNewPrintingsForCardAsync(printing).ConfigureAwait(false);
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
                    (string.IsNullOrWhiteSpace(rarityName) || RarityExtensions.NormalizeRarityName(s.RarityName) == rarityName)) == true);

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

        /// <summary>
        /// Places a recorded edition against what TCGPlayer lists for its printing: null when listed, a mismatch when only
        /// other editions are, and unverifiable when the printing has no linked TCGPlayer product (or has left the catalog).
        /// </summary>
        private static (EditionAuditCategory? Category, IReadOnlyList<CardEdition> AvailableEditions) CategorizeEdition(
            IReadOnlyDictionary<int, IReadOnlySet<CardEdition>> editionsByPrinting,
            int? printingID,
            CardEdition recordedEdition)
        {
            if (printingID is not int id || !editionsByPrinting.TryGetValue(id, out var listed) || listed.Count == 0)
                return (EditionAuditCategory.Unverifiable, []);

            var available = listed.OrderBy(e => e).ToList();
            return listed.Contains(recordedEdition) ? (null, available) : (EditionAuditCategory.EditionMismatch, available);
        }

        /// <summary>The value of each set in the snapshots, highest first, capped at the breakdown limit.</summary>
        private static IReadOnlyList<(string Label, decimal Value)> BuildSetValueBreakdown(IEnumerable<CollectionEntryValueSnapshot> snapshots) =>
            snapshots
                .GroupBy(s => s.SetName)
                .Select(g => (g.Key, g.Sum(s => s.MarketValue)))
                .OrderByDescending(x => x.Item2)
                .Take(SetBreakdownItemLimit)
                .ToList();

        /// <summary>The ten most valuable printings by per-copy value, from each entry's snapshot divided by its quantity.</summary>
        private static IReadOnlyList<(string CardName, string SetName, string RarityName, decimal Value)> BuildTopValueCards(
            IEnumerable<CollectionEntryValueSnapshot> snapshots,
            IEnumerable<CollectionEntry> entries)
        {
            var quantityByEntryID = entries.ToDictionary(e => e.ID, e => e.Quantity);
            return snapshots
                .Where(s => quantityByEntryID.TryGetValue(s.CollectionEntryID, out var quantity) && quantity > 0)
                .Select(s => (s.CardName, s.SetName, s.RarityName, UnitPrice: s.MarketValue / quantityByEntryID[s.CollectionEntryID]))
                .GroupBy(x => (x.CardName, x.SetName, x.RarityName))
                .Select(g => (g.Key.CardName, g.Key.SetName, g.Key.RarityName, g.First().UnitPrice))
                .OrderByDescending(x => x.Item4)
                .Take(10)
                .ToList();
        }

        /// <summary>
        /// The card's printings released after <paramref name="baselineDate"/> and by <paramref name="today"/> that haven't
        /// been dismissed, oldest first.
        /// </summary>
        private IReadOnlyList<NewPrintingOptionViewModel> BuildNewerPrintings(
            Card card,
            string baselineDate,
            string today,
            IReadOnlySet<int> dismissed,
            IReadOnlyDictionary<string, string> setNamesByCode,
            int? exceptPrintingID) =>
            (card.CardSets ?? [])
                .Where(s => s.PrintingID > 0 && s.PrintingID != exceptPrintingID && !dismissed.Contains(s.PrintingID))
                .Select(s => (Set: s, Date: _cardSetRepository.GetTCGDateBySetCode(s.Code ?? string.Empty)))
                .Where(x => x.Date is not null
                            && string.Compare(x.Date, baselineDate, StringComparison.Ordinal) > 0
                            && string.Compare(x.Date, today, StringComparison.Ordinal) <= 0)
                .Select(x => new NewPrintingOptionViewModel
                {
                    PrintingID = x.Set.PrintingID,
                    PrintVariant = x.Set.PrintVariant,
                    RarityName = RarityExtensions.NormalizeRarityName(x.Set.RarityName) ?? string.Empty,
                    ReleaseDate = x.Date,
                    SetCode = x.Set.Code ?? string.Empty,
                    SetName = setNamesByCode.TryGetValue(x.Set.Code ?? string.Empty, out var setName) ? setName : x.Set.Name ?? string.Empty
                })
                .OrderBy(o => o.ReleaseDate)
                .ThenBy(o => o.SetCode)
                .ToList();

        private static string GetSetPrefix(string code)
        {
            var hyphen = code.IndexOf('-');
            return hyphen > 0 ? code[..hyphen] : code;
        }

        private static List<ValueSnapshotPoint> MapValueHistory<T>(IEnumerable<T> snapshots) where T : IValueSnapshotEntity =>
            snapshots.Select(s => new ValueSnapshotPoint(s.SnapshotDate, s.TotalValue, s.Count)).ToList();

        /// <summary>
        /// Dismisses every printing of the card released after <paramref name="printing"/> (and not in the future), so
        /// choosing a printing doesn't immediately raise "newer printing" opportunities for ones the user just passed over.
        /// </summary>
        private async Task AutoDismissNewPrintingsForCardAsync(CatalogPrintingRef printing)
        {
            var cutoffDate = _cardSetRepository.GetTCGDateBySetCode(printing.Printing.Code ?? string.Empty);
            if (cutoffDate is null || printing.Card.CardSets is null)
                return;

            var today = DateTime.UtcNow.ToString(SnapshotDateFormat);
            var toDismiss = printing.Card.CardSets
                .Where(s => s.PrintingID > 0)
                .Select(s => (Set: s, Date: _cardSetRepository.GetTCGDateBySetCode(s.Code ?? string.Empty)))
                .Where(x => x.Date is not null
                            && string.Compare(x.Date, cutoffDate, StringComparison.Ordinal) > 0
                            && string.Compare(x.Date, today, StringComparison.Ordinal) <= 0)
                .ToList();

            foreach (var (set, _) in toDismiss)
                await _dismissedNewPrintingRepository.AddAsync(set.PrintingID, printing.Card.ID).ConfigureAwait(false);
        }

        private async Task<IReadOnlyDictionary<int, CollectionCompletionStatus?>> BuildCardCompletionMapAsync(IReadOnlyList<Card> cards)
        {
            var cardIDs = cards.Select(c => c.ID).ToList();
            var completionMap = await _collectionRepository.GetCompletionStatusByCardIDsAsync(cardIDs).ConfigureAwait(false);

            return cards.ToDictionary(
                c => c.ID,
                c => completionMap.TryGetValue(c.ID, out var s) ? (CollectionCompletionStatus?)s : null);
        }
        /// <summary>
        /// Builds the printing a user row points at. A row whose printing has left the catalog shows only its card, so it
        /// still appears (and can be fixed from the catalog admin pages) instead of vanishing.
        /// </summary>
        private CardPrinting BuildCardPrinting(IPrintingKeyedRow row)
        {
            if (row.CatalogPrintingID is int printingID && _cardDataRepository.GetPrintingByID(printingID) is { } printing)
                return BuildCardPrinting(printing);

            var card = _cardDataRepository.GetCardByID(row.CardID);
            return new CardPrinting
            {
                CardID = row.CardID,
                CardName = card?.Name ?? "Unknown",
                CardType = card?.CardType ?? string.Empty,
                ImageURLSmall = card?.CardImages?.FirstOrDefault()?.ImageURLSmall ?? string.Empty,
                SetName = "Printing no longer in the catalog"
            };
        }

        private static CardPrinting BuildCardPrinting(CatalogPrintingRef printing)
        {
            var (card, set) = (printing.Card, printing.Printing);
            var rarityName = RarityExtensions.NormalizeRarityName(set.RarityName) ?? string.Empty;
            return new CardPrinting
            {
                AvailableRarities = (card.CardSets ?? [])
                    .Where(s => s.Code == set.Code && !string.IsNullOrEmpty(s.RarityName))
                    .Select(s => RarityExtensions.NormalizeRarityName(s.RarityName)!)
                    .Distinct()
                    .OrderBy(r => r)
                    .ToList(),
                CardID = card.ID,
                CardName = card.Name ?? "Unknown",
                CardType = card.CardType ?? string.Empty,
                ImageURLSmall = card.CardImages?.FirstOrDefault()?.ImageURLSmall ?? string.Empty,
                Price = set.Price,
                PrintingID = set.PrintingID,
                PrintVariant = set.PrintVariant,
                RarityCode = RarityExtensions.GetRarityCode(rarityName) ?? string.Empty,
                RarityName = rarityName,
                SetCode = set.Code ?? string.Empty,
                SetName = set.Name ?? set.Code ?? string.Empty
            };
        }

        private async Task<PurchasePriorityCandidateViewModel?> EvaluateCandidateAsync(
            CatalogPrintingRef printing, DateTime asOf, decimal? maxPrice,
            IReadOnlyDictionary<int, int> ownedQuantities,
            IReadOnlyDictionary<int, int> orderedQuantities,
            IReadOnlyDictionary<int, int> stagedQuantities,
            int desiredQuantity = 3)
        {
            var card = printing.Card;
            var printingID = printing.Printing.PrintingID;
            var setCode = printing.Printing.Code ?? string.Empty;
            var candidate = PurchasePriorityAnalyzer.Evaluate(
                card, setCode, printing.Printing.RarityName, _cardSetRepository.GetTCGDateBySetCode, asOf)
                ?? new PurchasePriorityCandidate
                {
                    CardID = card.ID,
                    CardName = card.Name ?? string.Empty,
                    FoilCount = int.MaxValue // Sorts after every flagged candidate — nothing here to rank by.
                };

            var cardPrinting = BuildCardPrinting(printing);

            // Card data prices aren't maintained, so use live TCGPlayer pricing instead, defaulting to 1st Edition
            // since a tracked printing doesn't record which edition the user wants.
            var livePrice = await _pricingService.GetPrintingPriceAsync(printingID, CardEdition.FirstEdition).ConfigureAwait(false);
            if (maxPrice.HasValue && (!livePrice.HasValue || livePrice.Value <= 0 || livePrice.Value > maxPrice.Value))
                return null; // No price data or over the cap — exclude, don't let missing data slip under the cap

            // TCGPlayer mass entry matches by name + set prefix only (no rarity) — flag when another
            // rarity shares the prefix, since mass entry could silently resolve to the wrong one.
            var tcgSetCode = setCode.ToTCGPlayerSetCode();
            var hasAmbiguousSetCode = (card.CardSets ?? [])
                .Any(s => !string.IsNullOrEmpty(s.Code)
                    && s.Code.ToTCGPlayerSetCode().Equals(tcgSetCode, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(RarityExtensions.NormalizeRarityName(s.RarityName), cardPrinting.RarityName, StringComparison.OrdinalIgnoreCase));

            var candidateViewModel = PurchasePriorityCandidateViewModel.From(
                cardPrinting.WithPrice(livePrice),
                candidate,
                ownedQuantities.GetValueOrDefault(printingID),
                hasAmbiguousSetCode,
                stagedQuantities.GetValueOrDefault(printingID),
                orderedQuantities.GetValueOrDefault(printingID),
                desiredQuantity);

            // Already fully covered by what's owned, ordered, and staged — nothing left to recommend buying.
            return candidateViewModel.QuantityNeeded <= 0 ? null : candidateViewModel;
        }

        /// <summary>
        /// Returns the card IDs that have at least one Owned printing not matched by a tracked version --
        /// i.e. cards where the specific printing you own isn't the one you've tracked. Used by Browse's
        /// unscoped Tracked filter, which otherwise couldn't distinguish "never tracked" from "tracked as a
        /// different printing than what's owned."
        /// </summary>
        private async Task<IReadOnlySet<int>> GetCardIDsWithUntrackedOwnedPrintingsAsync()
        {
            var ownedPrintings = await _collectionRepository.GetOwnedCardPrintingsAsync().ConfigureAwait(false);
            if (ownedPrintings.Count == 0)
                return new HashSet<int>();

            var trackedByCardID = (await _preferredVersionRepository.GetAllAsync().ConfigureAwait(false))
                .GroupBy(pv => pv.CardID)
                .ToDictionary(g => g.Key, g => g.ToList());

            var result = new HashSet<int>();
            foreach (var (cardID, printingID) in ownedPrintings)
            {
                var isTracked = trackedByCardID.TryGetValue(cardID, out var trackedForCard)
                    && trackedForCard.Any(pv => pv.CatalogPrintingID == printingID);

                if (!isTracked)
                    result.Add(cardID);
            }

            return result;
        }

        /// <summary>Returns the active catalog printing, or throws when the ID names none.</summary>
        private CatalogPrintingRef GetRequiredPrinting(int printingID) =>
            _cardDataRepository.GetPrintingByID(printingID)
            ?? throw new ArgumentException($"{printingID} is not an active catalog printing.", nameof(printingID));

        private async Task<IReadOnlyList<EditionAuditResult>> GetEditionAuditResultsAsync()
        {
            var owned = await _collectionRepository.GetByStatusAsync(CollectionStatus.Owned).ConfigureAwait(false);
            var ordered = await _collectionRepository.GetByStatusAsync(CollectionStatus.Ordered).ConfigureAwait(false);

            var results = new List<EditionAuditResult>();
            foreach (var group in owned.Concat(ordered).Where(e => e.Edition.HasValue).GroupBy(e => e.CardID))
            {
                var editions = await _pricingService.GetEditionsByPrintingAsync(group.Key).ConfigureAwait(false);

                foreach (var entry in group)
                {
                    var recordedEdition = entry.Edition!.Value;
                    var (category, availableEditions) = CategorizeEdition(editions, entry.CatalogPrintingID, recordedEdition);
                    if (category is not null)
                        results.Add(EditionAuditResult.From(BuildCardPrinting(entry), entry.ID, recordedEdition, availableEditions, category.Value));
                }
            }

            return results;
        }

        private async Task<IEnumerable<OrderEntryViewModel>> GetEnrichedByStatusAsync(CollectionStatus status)
        {
            var entries = await _collectionRepository.GetByStatusAsync(status).ConfigureAwait(false);
            var viewModels = new List<OrderEntryViewModel>();

            foreach (var entry in entries)
                viewModels.Add(OrderEntryViewModel.From(BuildCardPrinting(entry), entry));

            return viewModels;
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
                        var baseEntry = OrderEntryViewModel.From(BuildCardPrinting(e), e);
                        var isFlagged = flaggedByEntryID.TryGetValue(e.ID, out var flagged);
                        return EditionAuditEntryViewModel.From(
                            baseEntry,
                            isFlagged ? flagged!.Category : null,
                            isFlagged ? flagged!.AvailableEditions : []);
                    })
                    .ToList();

                foreach (var group in enrichedEntries.GroupBy(e => e.PrintingID > 0 ? $"printing:{e.PrintingID}" : $"entry:{e.EntryID}"))
                {
                    if (!group.Any(e => e.Category.HasValue))
                        continue;

                    groups.Add(EditionAuditGroupViewModel.From(group.First(), group.ToList()));
                }
            }

            return groups;
        }

        private async Task<(
            IReadOnlyDictionary<int, int> OwnedQuantities,
            IReadOnlyDictionary<int, int> OrderedQuantities,
            IReadOnlySet<int> TrackedCardIDs,
            IReadOnlyDictionary<int, int> MaxDesiredQuantities)> GetPrintingScopedDataAsync(
            IReadOnlyList<int> cardIDs, string? setPrefix, string? rarityName, bool needOwned, bool needOrdered, bool needTracked)
        {
            bool InScope(int? printingID) =>
                printingID is int id
                && _cardDataRepository.GetPrintingByID(id) is { } printing
                && (string.IsNullOrWhiteSpace(setPrefix) || GetSetPrefix(printing.Printing.Code ?? string.Empty).Equals(setPrefix, StringComparison.OrdinalIgnoreCase))
                && (string.IsNullOrWhiteSpace(rarityName) || string.Equals(RarityExtensions.NormalizeRarityName(printing.Printing.RarityName), rarityName, StringComparison.OrdinalIgnoreCase));

            async Task<IReadOnlyDictionary<int, int>> QuantitiesByCardAsync(CollectionStatus status)
            {
                var byPrinting = await _collectionRepository.GetQuantitiesByPrintingAsync(cardIDs, status).ConfigureAwait(false);
                return byPrinting
                    .Where(kvp => InScope(kvp.Key))
                    .GroupBy(kvp => _cardDataRepository.GetPrintingByID(kvp.Key)!.Card.ID)
                    .ToDictionary(g => g.Key, g => g.Sum(kvp => kvp.Value));
            }

            var ownedQuantities = needOwned ? await QuantitiesByCardAsync(CollectionStatus.Owned).ConfigureAwait(false) : new Dictionary<int, int>();
            var orderedQuantities = needOrdered ? await QuantitiesByCardAsync(CollectionStatus.Ordered).ConfigureAwait(false) : new Dictionary<int, int>();

            var trackedCardIDs = new HashSet<int>();
            var maxDesiredQuantities = new Dictionary<int, int>();

            if (needTracked)
            {
                var allTracked = await _preferredVersionRepository.GetAllAsync().ConfigureAwait(false);
                foreach (var group in allTracked.Where(pv => InScope(pv.CatalogPrintingID)).GroupBy(pv => pv.CardID))
                {
                    trackedCardIDs.Add(group.Key);
                    maxDesiredQuantities[group.Key] = group.Max(pv => pv.DesiredQuantity);
                }
            }

            return (ownedQuantities, orderedQuantities, trackedCardIDs, maxDesiredQuantities);
        }
    }
}
