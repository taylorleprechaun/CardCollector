using System.Globalization;
using CardCollector.Data.Models;
using CardCollector.Data.Models.Catalog;
using CardCollector.DTO;
using CardCollector.Models;
using CardCollector.Models.Catalog;
using CardCollector.Models.Legacy;
using CardCollector.Repository;
using CardCollector.Repository.Catalog;
using CardCollector.Repository.Legacy;
using CardCollector.Rules.Catalog;
using CardCollector.ViewModels;
using Newtonsoft.Json.Linq;

namespace CardCollector.Services
{
    public sealed class CatalogReviewService : ICatalogReviewService
    {
        private static readonly IReadOnlyList<CatalogReviewItemKind> ActionableKinds =
            Enum.GetValues<CatalogReviewItemKind>().Where(k => k.IsActionable()).ToList();

        private static readonly IReadOnlyList<CatalogReviewItemStatus> ClosedStatuses =
            Enum.GetValues<CatalogReviewItemStatus>().Where(s => s != CatalogReviewItemStatus.Open).ToList();

        private static readonly IReadOnlyList<CatalogReviewItemKind> InformationalKinds =
            Enum.GetValues<CatalogReviewItemKind>().Where(k => !k.IsActionable()).ToList();

        private readonly ICardDataRepository _cardDataRepository;
        private readonly ILegacyImportRepository _importRepository;
        private readonly ILogger<CatalogReviewService> _logger;
        private readonly ICatalogReviewRepository _reviewRepository;
        private readonly TimeProvider _timeProvider;
        private readonly IUnitOfWork _unitOfWork;

        public CatalogReviewService(
            ICardDataRepository cardDataRepository,
            ILegacyImportRepository importRepository,
            ILogger<CatalogReviewService> logger,
            ICatalogReviewRepository reviewRepository,
            IUnitOfWork unitOfWork,
            TimeProvider? timeProvider = null)
        {
            _cardDataRepository = cardDataRepository;
            _importRepository = importRepository;
            _logger = logger;
            _reviewRepository = reviewRepository;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider ?? TimeProvider.System;
        }

        public async Task<SaveResult> AcceptAsync(int id, CancellationToken cancellationToken = default)
        {
            var item = await _reviewRepository.GetItemAsync(id, cancellationToken).ConfigureAwait(false);
            if (item is null || item.Status != CatalogReviewItemStatus.Open)
                return SaveResult.Missing("That review item is not open.");
            if (!CatalogReviewPresenter.AllowedActions(item.Kind, item.Status).Contains(CatalogReviewAction.Accept))
                return SaveResult.Failure([$"{item.Kind} items can't be accepted."]);

            var payload = JObject.Parse(item.PayloadJSON);
            var now = Now();
            string resolution;
            switch (item.Kind)
            {
                case CatalogReviewItemKind.PasscodeChanged:
                {
                    var card = await _reviewRepository.GetCardAsync(item.CatalogCardID ?? 0, cancellationToken).ConfigureAwait(false);
                    if (card is null || !int.TryParse(CatalogReviewPresenter.ReadString(payload, "Reported"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var reported))
                        return SaveResult.Missing("The card or the reported passcode is missing.");
                    resolution = $"Passcode changed from {card.Passcode} to {reported}";
                    card.Passcode = reported;
                    card.DateModified = now;
                    break;
                }
                case CatalogReviewItemKind.CardNameChanged:
                {
                    var card = await _reviewRepository.GetCardAsync(item.CatalogCardID ?? 0, cancellationToken).ConfigureAwait(false);
                    var reported = CatalogReviewPresenter.ReadString(payload, "Reported");
                    if (card is null || string.IsNullOrWhiteSpace(reported))
                        return SaveResult.Missing("The card or the reported name is missing.");
                    resolution = $"Renamed from \"{card.Name}\" to \"{reported}\"";
                    card.Name = reported;
                    card.DateModified = now;
                    break;
                }
                case CatalogReviewItemKind.CardNotSeen:
                {
                    var card = await _reviewRepository.GetCardAsync(item.CatalogCardID ?? 0, cancellationToken).ConfigureAwait(false);
                    if (card is null)
                        return SaveResult.Missing("The card no longer exists.");
                    var inUse = await _reviewRepository.CountUserRowsForCardAsync(card.ID, cancellationToken).ConfigureAwait(false);
                    if (inUse > 0)
                        return SaveResult.Failure([$"{inUse} of your collection, wishlist, checked-out, or cart rows still use this card, so it can't be retired."]);
                    card.Status = CatalogStatus.Retired;
                    card.DateModified = now;
                    resolution = "Card retired";
                    break;
                }
                default:
                {
                    var printing = await _reviewRepository.GetPrintingAsync(item.CatalogPrintingID ?? 0, cancellationToken).ConfigureAwait(false);
                    if (printing is null)
                        return SaveResult.Missing("The printing no longer exists.");
                    var inUse = await _reviewRepository.CountUserRowsForPrintingAsync(printing.ID, cancellationToken).ConfigureAwait(false);
                    if (inUse > 0)
                        return SaveResult.Failure([$"{inUse} of your collection, wishlist, checked-out, or cart rows still use this printing, so it can't be retired."]);
                    printing.Status = CatalogStatus.Retired;
                    printing.DateModified = now;
                    resolution = $"Printing {CatalogPrintingFactory.Describe(printing)} retired";
                    break;
                }
            }

            Close(item, CatalogReviewItemStatus.Accepted, resolution, now);
            await _reviewRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            _cardDataRepository.QueueReload();
            return SaveResult.Success();
        }

        public async Task<int> CountNeedingDecisionAsync(CancellationToken cancellationToken = default) =>
            await _reviewRepository.CountOpenAsync(ActionableKinds.ToList(), cancellationToken).ConfigureAwait(false);

        public async Task<SaveResult> CreateCardAsync(int id, CancellationToken cancellationToken = default)
        {
            var item = await _reviewRepository.GetItemAsync(id, cancellationToken).ConfigureAwait(false);
            if (item is null || item.Status != CatalogReviewItemStatus.Open)
                return SaveResult.Missing("That review item is not open.");
            if (!CatalogReviewPresenter.AllowedActions(item.Kind, item.Status).Contains(CatalogReviewAction.CreateCard))
                return SaveResult.Failure([$"{item.Kind} items can't create a card."]);

            var payload = JObject.Parse(item.PayloadJSON);
            var name = CatalogReviewPresenter.ReadString(payload, "CardName")?.Trim();
            var setCode = CatalogReviewPresenter.ReadString(payload, "SetCode");
            var rawRarity = CatalogReviewPresenter.ReadString(payload, "RarityName");
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(setCode) || string.IsNullOrWhiteSpace(rawRarity))
                return SaveResult.Failure(["The item has no card name, set code, or rarity to create a card from."]);

            var existing = await _reviewRepository.FindCardByNameAsync(name, cancellationToken).ConfigureAwait(false);
            if (existing is not null)
                return SaveResult.Failure([existing.Status == CatalogStatus.Retired
                    ? $"A retired catalog card is named \"{existing.Name}\"; restore it in the catalog admin, then create the printing on it."
                    : $"The catalog already has a card named \"{existing.Name}\"; create the printing on it instead."]);

            var (rarity, variant) = CatalogReviewPresenter.NormalizeRarityAndVariant(rawRarity, CatalogReviewPresenter.ReadString(payload, "PrintVariant"));
            SaveResult result = SaveResult.Success();
            try
            {
                await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    var now = Now();
                    var card = new CatalogCard
                    {
                        DateCreated = now,
                        DateModified = now,
                        FirstSeenUTC = now,
                        LastSeenUTC = now,
                        Name = name,
                        Status = CatalogStatus.Active
                    };
                    _reviewRepository.AddCard(card);

                    var prefix = CardDataMapper.GetSetPrefix(setCode);
                    var set = await _reviewRepository.GetSetByPrefixAsync(prefix, cancellationToken).ConfigureAwait(false);
                    if (set is null)
                    {
                        set = new CatalogSet { DateCreated = now, DateModified = now, Name = prefix, Prefix = prefix };
                        _reviewRepository.AddSet(set);
                    }

                    var printing = CatalogPrintingFactory.Create(card, setCode, set.Name, rarity, variant, set, now);
                    result = await PlaceOnPrintingAsync(item, payload, printing, cancellationToken).ConfigureAwait(false);
                    if (!result.Succeeded)
                        throw new CatalogReviewDecisionException(result);

                    item.CatalogCardID = card.ID;
                    item.CatalogPrintingID = printing.ID;
                    item.Resolution = $"Created card \"{name}\" with {CatalogPrintingFactory.Describe(printing)}";
                    await _reviewRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    _logger.LogInformation("Created catalog card {CardID} ({Name}) from TCGPlayer review item {ItemID}", card.ID, name, item.ID);
                }, cancellationToken).ConfigureAwait(false);
            }
            catch (CatalogReviewDecisionException failed)
            {
                return failed.Result;
            }

            return FinishPlacement(result);
        }

        public async Task<SaveResult> CreatePrintingAsync(int id, int catalogCardID, CancellationToken cancellationToken = default)
        {
            var item = await _reviewRepository.GetItemAsync(id, cancellationToken).ConfigureAwait(false);
            if (item is null || item.Status != CatalogReviewItemStatus.Open)
                return SaveResult.Missing("That review item is not open.");
            if (!CatalogReviewPresenter.AllowedActions(item.Kind, item.Status).Contains(CatalogReviewAction.CreatePrinting))
                return SaveResult.Failure([$"{item.Kind} items can't create a printing."]);

            var card = await _reviewRepository.GetCardAsync(catalogCardID, cancellationToken).ConfigureAwait(false);
            if (card is null)
                return SaveResult.Missing("That card no longer exists.");

            var payload = JObject.Parse(item.PayloadJSON);
            var source = item.Kind == CatalogReviewItemKind.UnmappedTCGProduct ? payload : payload["Row"] as JObject ?? new JObject();
            var setCode = CatalogReviewPresenter.ReadString(source, "SetCode");
            var rawRarity = CatalogReviewPresenter.ReadString(source, "RarityName");
            if (string.IsNullOrWhiteSpace(setCode) || string.IsNullOrWhiteSpace(rawRarity))
                return SaveResult.Failure(["The item has no set code or rarity to create a printing from."]);

            var (rarity, variant) = CatalogReviewPresenter.NormalizeRarityAndVariant(rawRarity, CatalogReviewPresenter.ReadString(source, "PrintVariant"));

            SaveResult result = SaveResult.Success();
            try
            {
                await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    var printing = card.Printings.FirstOrDefault(p =>
                        string.Equals(p.SetCode, setCode, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(p.RarityName, rarity, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(p.PrintVariant, variant, StringComparison.OrdinalIgnoreCase));
                    if (printing is null)
                    {
                        var prefix = CardDataMapper.GetSetPrefix(setCode);
                        var sibling = card.Printings.FirstOrDefault(p => string.Equals(p.SetCode, setCode, StringComparison.OrdinalIgnoreCase));
                        var set = sibling?.Set ?? await _reviewRepository.GetSetByPrefixAsync(prefix, cancellationToken).ConfigureAwait(false);
                        if (set is null)
                        {
                            set = new CatalogSet { DateCreated = Now(), DateModified = Now(), Name = prefix, Prefix = prefix };
                            _reviewRepository.AddSet(set);
                        }

                        printing = CatalogPrintingFactory.Create(card, setCode, sibling?.SetName ?? set.Name, rarity, variant, set, Now());
                        await _reviewRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    }

                    result = await PlaceOnPrintingAsync(item, payload, printing, cancellationToken).ConfigureAwait(false);
                    if (!result.Succeeded)
                        throw new CatalogReviewDecisionException(result);
                }, cancellationToken).ConfigureAwait(false);
            }
            catch (CatalogReviewDecisionException failed)
            {
                return failed.Result;
            }

            return FinishPlacement(result);
        }

        public async Task<int> DismissAllInformationalAsync(CatalogReviewItemKind? kind, string? query = null, CancellationToken cancellationToken = default)
        {
            var kinds = kind is CatalogReviewItemKind only
                ? InformationalKinds.Where(k => k == only).ToList()
                : InformationalKinds.ToList();
            if (kinds.Count == 0)
                return 0;

            var items = await _reviewRepository.GetOpenItemsAsync(kinds, query, cancellationToken).ConfigureAwait(false);
            var now = Now();
            foreach (var item in items)
                Close(item, CatalogReviewItemStatus.Dismissed, "Dismissed with the rest of its kind", now);

            await _reviewRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Dismissed {Count} informational review items (kind {Kind}, search {Query})", items.Count, kind?.ToString() ?? "all", query ?? "none");
            return items.Count;
        }

        public async Task<SaveResult> DismissAsync(int id, CancellationToken cancellationToken = default)
        {
            var item = await _reviewRepository.GetItemAsync(id, cancellationToken).ConfigureAwait(false);
            if (item is null || item.Status != CatalogReviewItemStatus.Open)
                return SaveResult.Missing("That review item is not open.");
            if (!CatalogReviewPresenter.AllowedActions(item.Kind, item.Status).Contains(CatalogReviewAction.Dismiss))
                return SaveResult.Failure([$"{item.Kind} items need a decision, not a dismissal."]);

            Close(item, CatalogReviewItemStatus.Dismissed, "Dismissed", Now());
            await _reviewRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return SaveResult.Success();
        }
        public async Task<CatalogReviewItemViewModel?> GetItemAsync(int id, string? errorMessage = null, CancellationToken cancellationToken = default)
        {
            var item = await _reviewRepository.GetItemAsync(id, cancellationToken).ConfigureAwait(false);
            return item is null ? null : CatalogReviewPresenter.ToViewModel(item, BuildLookup(), errorMessage);
        }

        public async Task<IReadOnlyDictionary<CatalogReviewItemKind, int>> GetOpenCountsByKindAsync(CancellationToken cancellationToken = default) =>
            await _reviewRepository.CountOpenByKindAsync(cancellationToken).ConfigureAwait(false);

        public async Task<SaveResult> MapToPrintingAsync(int id, int catalogPrintingID, CancellationToken cancellationToken = default)
        {
            var item = await _reviewRepository.GetItemAsync(id, cancellationToken).ConfigureAwait(false);
            if (item is null || item.Status != CatalogReviewItemStatus.Open)
                return SaveResult.Missing("That review item is not open.");
            if (!CatalogReviewPresenter.AllowedActions(item.Kind, item.Status).Contains(CatalogReviewAction.MapToPrinting))
                return SaveResult.Failure([$"{item.Kind} items can't be mapped to a printing."]);

            var printing = await _reviewRepository.GetPrintingAsync(catalogPrintingID, cancellationToken).ConfigureAwait(false);
            if (printing is null)
                return SaveResult.Missing("That printing no longer exists.");

            var payload = JObject.Parse(item.PayloadJSON);
            SaveResult result = SaveResult.Success();
            try
            {
                await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    result = await PlaceOnPrintingAsync(item, payload, printing, cancellationToken).ConfigureAwait(false);
                    if (!result.Succeeded)
                        throw new CatalogReviewDecisionException(result);
                }, cancellationToken).ConfigureAwait(false);
            }
            catch (CatalogReviewDecisionException failed)
            {
                return failed.Result;
            }

            return FinishPlacement(result);
        }

        public async Task<SaveResult> RejectAsync(int id, CancellationToken cancellationToken = default)
        {
            var item = await _reviewRepository.GetItemAsync(id, cancellationToken).ConfigureAwait(false);
            if (item is null || item.Status != CatalogReviewItemStatus.Open)
                return SaveResult.Missing("That review item is not open.");
            if (!CatalogReviewPresenter.AllowedActions(item.Kind, item.Status).Contains(CatalogReviewAction.Reject))
                return SaveResult.Failure([$"{item.Kind} items can't be rejected."]);

            var resolution = item.Kind switch
            {
                CatalogReviewItemKind.LegacyRowUnmapped => "Legacy row discarded (its values stay on this item)",
                CatalogReviewItemKind.UnmappedTCGProduct => "Product ignored",
                _ => "Kept the catalog as it was"
            };
            Close(item, CatalogReviewItemStatus.Rejected, resolution, Now());
            await _reviewRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return SaveResult.Success();
        }

        public async Task<SaveResult> ReopenAsync(int id, CancellationToken cancellationToken = default)
        {
            var item = await _reviewRepository.GetItemAsync(id, cancellationToken).ConfigureAwait(false);
            if (item is null)
                return SaveResult.Missing("That review item no longer exists.");
            if (!CatalogReviewPresenter.AllowedActions(item.Kind, item.Status).Contains(CatalogReviewAction.Reopen))
                return SaveResult.Failure(["Only rejected or dismissed items can be reopened."]);

            item.Status = CatalogReviewItemStatus.Open;
            item.Resolution = null;
            item.ResolvedUTC = null;
            item.DateModified = Now();
            await _reviewRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Reopened catalog review item {ItemID} ({Kind})", item.ID, item.Kind);
            return SaveResult.Success();
        }

        public async Task<PagedResult<CatalogReviewItemViewModel>> SearchAsync(CatalogReviewSearchCriteria criteria, CancellationToken cancellationToken = default)
        {
            if (criteria is null) throw new ArgumentNullException(nameof(criteria));

            IReadOnlyList<CatalogReviewItemStatus> statuses = criteria.Filter == CatalogReviewFilter.Resolved
                ? ClosedStatuses
                : [CatalogReviewItemStatus.Open];
            var kinds = criteria.Filter switch
            {
                CatalogReviewFilter.NeedsDecision => ActionableKinds,
                CatalogReviewFilter.Informational => InformationalKinds,
                _ => Enum.GetValues<CatalogReviewItemKind>()
            };
            if (criteria.Kind is CatalogReviewItemKind kind)
                kinds = kinds.Where(k => k == kind).ToList();

            var page = Paging.ClampPage(criteria.Page);
            var pageSize = Paging.ClampPageSize(criteria.PageSize);
            var (items, totalCount) = await _reviewRepository
                .SearchAsync(statuses.ToList(), kinds.ToList(), criteria.Query, page, pageSize, cancellationToken)
                .ConfigureAwait(false);

            var lookup = BuildLookup();
            return new PagedResult<CatalogReviewItemViewModel>
            {
                Items = items.Select(i => CatalogReviewPresenter.ToViewModel(i, lookup)).ToList(),
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        private static void Close(CatalogReviewItem item, CatalogReviewItemStatus status, string resolution, DateTime now)
        {
            item.Status = status;
            item.Resolution = resolution;
            item.ResolvedUTC = now;
            item.DateModified = now;
        }

        private CatalogReviewLookup BuildLookup() => new()
        {
            CardByID = _cardDataRepository.GetCardByID,
            CardByPasscode = _cardDataRepository.GetCardByPasscode,
            CardsByName = _cardDataRepository.GetCardsByName,
            PrintingsBySetCode = _cardDataRepository.GetPrintingsBySetCode
        };

        private SaveResult FinishPlacement(SaveResult result)
        {
            if (result.Succeeded)
                _cardDataRepository.QueueReload();
            return result;
        }

        private DateTime Now() => _timeProvider.GetUtcNow().UtcDateTime;

        /// <summary>
        /// Links the item's product to the printing, or restores its legacy row on the printing (folding into an existing
        /// row with the same unique key, as the migration does), then closes the item. Runs inside the caller's transaction.
        /// </summary>
        private async Task<SaveResult> PlaceOnPrintingAsync(CatalogReviewItem item, JObject payload, CatalogPrinting printing, CancellationToken cancellationToken)
        {
            var now = Now();
            var described = CatalogPrintingFactory.Describe(printing);
            if (item.Kind == CatalogReviewItemKind.UnmappedTCGProduct)
            {
                var productID = CatalogReviewPresenter.ReadString(payload, "ProductID");
                if (string.IsNullOrWhiteSpace(productID))
                    return SaveResult.Failure(["The item has no product id."]);
                if (await _reviewRepository.IsSourceLinkedAsync(CatalogSource.TCGPlayer, productID, cancellationToken).ConfigureAwait(false))
                    return SaveResult.Failure([$"TCGPlayer product {productID} is already linked to a printing."]);

                printing.Sources.Add(new CatalogPrintingSource
                {
                    DateCreated = now,
                    DateModified = now,
                    ExternalKey = productID,
                    ExternalSummary = string.Join(" | ", new[] { "CardName", "SetCode", "RarityName", "PrintVariant" }.Select(p => CatalogReviewPresenter.ReadString(payload, p))),
                    FirstSeenUTC = now,
                    LastSeenUTC = now,
                    Source = CatalogSource.TCGPlayer
                });
                Close(item, CatalogReviewItemStatus.Accepted, $"Linked to {described}", now);
                await _reviewRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return SaveResult.Success();
            }

            var restored = await RestoreLegacyRowAsync(payload, printing, cancellationToken).ConfigureAwait(false);
            if (!restored.Succeeded)
                return restored;

            Close(item, CatalogReviewItemStatus.Accepted, $"Restored on {described}", now);
            await _reviewRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return SaveResult.Success();
        }

        /// <summary>Recreates an unmapped legacy row on the chosen printing from the copy of it kept on the review item.</summary>
        private async Task<SaveResult> RestoreLegacyRowAsync(JObject payload, CatalogPrinting printing, CancellationToken cancellationToken)
        {
            var table = CatalogReviewPresenter.ReadString(payload, "Table");
            var values = (payload["Row"] as JObject)?.ToObject<Dictionary<string, object?>>();
            if (string.IsNullOrWhiteSpace(table) || values is null)
                return SaveResult.Failure(["The item doesn't carry the legacy row to restore."]);

            var row = new LegacyRow(new Dictionary<string, object?>(values, StringComparer.OrdinalIgnoreCase));
            var target = new LegacyPrintingTarget(printing.ID, printing.CatalogCardID, printing.SetCode, printing.RarityName, printing.PrintVariant);

            switch (table)
            {
                case "CollectionEntries":
                {
                    var entry = LegacyRowImporter.BuildCollectionEntry(row, target);
                    if (await _importRepository.FindAsync<CollectionEntry>(e => e.ID == entry.ID, cancellationToken).ConfigureAwait(false) is not null)
                        entry.ID = 0;
                    _importRepository.AddRange([entry]);
                    return SaveResult.Success();
                }
                case "PendingOrderLines":
                {
                    var line = LegacyRowImporter.BuildPendingOrderLine(row, target);
                    if (await _importRepository.FindAsync<PendingOrderLine>(l => l.ID == line.ID, cancellationToken).ConfigureAwait(false) is not null)
                        line.ID = 0;
                    _importRepository.AddRange([line]);
                    return SaveResult.Success();
                }
                case "PreferredVersions":
                {
                    var version = LegacyRowImporter.BuildPreferredVersion(row, target);
                    var existing = await _importRepository.FindAsync<PreferredVersion>(p => p.CatalogPrintingID == target.PrintingID, cancellationToken).ConfigureAwait(false);
                    if (existing is not null)
                    {
                        existing.DesiredQuantity = Math.Max(existing.DesiredQuantity, version.DesiredQuantity);
                        return SaveResult.Success();
                    }

                    if (await _importRepository.FindAsync<PreferredVersion>(p => p.ID == version.ID, cancellationToken).ConfigureAwait(false) is not null)
                        version.ID = 0;
                    _importRepository.AddRange([version]);
                    return SaveResult.Success();
                }
                case "CheckedOutCards":
                {
                    var checkedOut = LegacyRowImporter.BuildCheckedOutCard(row, target);
                    var existing = await _importRepository.FindAsync<CheckedOutCard>(c => c.CatalogPrintingID == target.PrintingID, cancellationToken).ConfigureAwait(false);
                    if (existing is not null)
                    {
                        existing.Quantity += checkedOut.Quantity;
                        return SaveResult.Success();
                    }

                    if (await _importRepository.FindAsync<CheckedOutCard>(c => c.ID == checkedOut.ID, cancellationToken).ConfigureAwait(false) is not null)
                        checkedOut.ID = 0;
                    _importRepository.AddRange([checkedOut]);
                    return SaveResult.Success();
                }
                default:
                    return SaveResult.Failure([$"Rows from {table} can't be restored on a printing."]);
            }
        }
    }
}
