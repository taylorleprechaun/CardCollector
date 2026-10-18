using CardCollector.Data.Models.Catalog;
using CardCollector.Models;
using CardCollector.Models.Catalog;
using CardCollector.Repository;
using CardCollector.Repository.Catalog;
using CardCollector.Rules.Catalog;
using CardCollector.ViewModels;
using Microsoft.Extensions.Options;

namespace CardCollector.Services
{
    public sealed class CatalogAdminService : ICatalogAdminService
    {
        /// <summary>How many recent runs are read to measure a source's failure streak.</summary>
        private const int STREAK_RUNS_READ = 50;

        private readonly ICatalogAdminRepository _adminRepository;
        private readonly ICardDataRepository _cardDataRepository;
        private readonly ILogger<CatalogAdminService> _logger;
        private readonly CatalogSyncSettings _settings;
        private readonly TimeProvider _timeProvider;
        private readonly IUnitOfWork _unitOfWork;

        public CatalogAdminService(
            ICatalogAdminRepository adminRepository,
            ICardDataRepository cardDataRepository,
            ILogger<CatalogAdminService> logger,
            IOptions<CatalogSyncSettings> settings,
            IUnitOfWork unitOfWork,
            TimeProvider? timeProvider = null)
        {
            _adminRepository = adminRepository;
            _cardDataRepository = cardDataRepository;
            _logger = logger;
            _settings = settings.Value;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider ?? TimeProvider.System;
        }

        public async Task<SaveResult> AddPrintingAsync(int catalogCardID, CatalogPrintingInput input, CancellationToken cancellationToken = default)
        {
            if (input is null) throw new ArgumentNullException(nameof(input));

            var setCode = input.SetCode?.Trim().ToUpperInvariant();
            var rawRarity = input.RarityName?.Trim();
            if (string.IsNullOrEmpty(setCode) || string.IsNullOrEmpty(rawRarity))
                return SaveResult.Failure(["A printing needs a set code and a rarity."]);

            var card = await _adminRepository.GetCardAsync(catalogCardID, cancellationToken).ConfigureAwait(false);
            if (card is null)
                return SaveResult.Missing("That card no longer exists.");

            var (rarity, variant) = CatalogPrintingFactory.NormalizeRarityAndVariant(rawRarity, input.PrintVariant?.Trim());

            var existing = card.Printings.FirstOrDefault(p =>
                string.Equals(p.SetCode, setCode, StringComparison.OrdinalIgnoreCase)
                && string.Equals(p.RarityName, rarity, StringComparison.OrdinalIgnoreCase)
                && string.Equals(p.PrintVariant, variant, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
                return SaveResult.Failure([existing.Status == CatalogStatus.Retired
                    ? $"{CatalogPrintingFactory.Describe(existing)} already exists but is retired; restore it instead."
                    : $"{CatalogPrintingFactory.Describe(existing)} already exists."]);

            var printing = await CatalogPrintingFactory.CreateByHandAsync(
                card,
                setCode,
                rarity,
                variant,
                "Added by hand",
                prefix => _adminRepository.GetSetByPrefixAsync(prefix, cancellationToken),
                _adminRepository.AddSet,
                Now()).ConfigureAwait(false);

            await _adminRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Added printing {Printing} to catalog card {CardID} by hand", CatalogPrintingFactory.Describe(printing), card.ID);
            _cardDataRepository.QueueReload();
            return SaveResult.Success();
        }

        public async Task<CatalogCardDetailsViewModel?> GetCardAsync(int catalogCardID, CancellationToken cancellationToken = default)
        {
            var card = await _adminRepository.GetCardAsync(catalogCardID, cancellationToken).ConfigureAwait(false);
            if (card is null)
                return null;

            var rowCounts = await _adminRepository.CountUserRowsByPrintingAsync(card.ID, cancellationToken).ConfigureAwait(false);
            var activePrintings = card.Printings
                .Where(p => p.Status == CatalogStatus.Active)
                .OrderBy(p => p.SetCode, StringComparer.OrdinalIgnoreCase)
                .ThenBy(p => p.RarityName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(p => p.PrintVariant, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var printings = activePrintings
                .Concat(card.Printings.Where(p => p.Status != CatalogStatus.Active).OrderBy(p => p.SetCode, StringComparer.OrdinalIgnoreCase))
                .Select(p => new CatalogPrintingAdminViewModel
                {
                    Description = CatalogPrintingFactory.Describe(p),
                    FirstSeenUTC = p.FirstSeenUTC,
                    ID = p.ID,
                    IsManual = p.Sources.Any(s => s.Source == CatalogSource.Manual),
                    LastSeenUTC = p.LastSeenUTC,
                    MergeTargets = activePrintings
                        .Where(t => t.ID != p.ID)
                        .Select(t => new CatalogReviewOption(t.ID, CatalogPrintingFactory.Describe(t)))
                        .ToList(),
                    PrintVariant = p.PrintVariant,
                    RarityName = p.RarityName,
                    SetCode = p.SetCode,
                    SetName = p.SetName,
                    Sources = p.Sources
                        .OrderBy(s => s.Source)
                        .ThenBy(s => s.ExternalKey, StringComparer.Ordinal)
                        .Select(s => new CatalogPrintingSourceViewModel
                        {
                            CanUnlink = s.Source == CatalogSource.TCGPlayer,
                            ExternalKey = s.ExternalKey,
                            ExternalSummary = s.ExternalSummary,
                            ID = s.ID,
                            LastSeenUTC = s.LastSeenUTC,
                            Source = s.Source
                        })
                        .ToList(),
                    Status = p.Status,
                    UserRowCount = rowCounts.GetValueOrDefault(p.ID)
                })
                .ToList();

            return new CatalogCardDetailsViewModel
            {
                Aliases = card.Aliases
                    .OrderBy(a => a.Kind.ResolutionPriority())
                    .ThenByDescending(a => a.LastSeenUTC)
                    .Select(a => new CatalogCardAliasViewModel(a.Source, a.Kind, a.Value, a.FirstSeenUTC, a.LastSeenUTC))
                    .ToList(),
                Fields = new CatalogCardEditInput
                {
                    Archetype = card.Archetype,
                    ATK = card.ATK,
                    Attribute = card.Attribute,
                    CardType = card.CardType,
                    DEF = card.DEF,
                    Description = card.Description,
                    Level = card.Level,
                    LinkMarkers = card.LinkMarkers,
                    LinkRating = card.LinkRating,
                    Name = card.Name,
                    Passcode = card.Passcode,
                    Type = card.Type,
                    TypeLine = card.TypeLine
                },
                FirstSeenUTC = card.FirstSeenUTC,
                ID = card.ID,
                ImageURLSmall = card.Images.OrderBy(i => i.SortOrder).FirstOrDefault()?.ImageURLSmall,
                KonamiID = card.KonamiID,
                LastSeenUTC = card.LastSeenUTC,
                LockedFields = CatalogLockedFields.Split(card.LockedFields),
                Name = card.Name,
                Printings = printings,
                RarityNames = _cardDataRepository.AllRarityNames,
                Status = card.Status,
                UserRowCount = rowCounts.Values.Sum()
            };
        }

        public async Task<CatalogSyncHistoryViewModel> GetSyncHistoryAsync(int page, int pageSize, CancellationToken cancellationToken = default)
        {
            page = Paging.ClampPage(page);
            pageSize = Paging.ClampPageSize(pageSize);

            var (runs, totalCount) = await _adminRepository.GetSyncRunsAsync(page, pageSize, cancellationToken).ConfigureAwait(false);
            var (recent, _) = await _adminRepository.GetSyncRunsAsync(1, STREAK_RUNS_READ, cancellationToken).ConfigureAwait(false);

            return new CatalogSyncHistoryViewModel
            {
                FailureStreaks = CatalogSyncHealth.FindFailureStreaks(recent, _settings.SourceFailureWarningRuns),
                Runs = new PagedResult<CatalogSyncRunViewModel>
                {
                    Items = runs.Select(ToViewModel).ToList(),
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalCount
                },
                WarningRuns = _settings.SourceFailureWarningRuns
            };
        }

        public async Task<SaveResult> MergePrintingAsync(int fromPrintingID, int intoPrintingID, CancellationToken cancellationToken = default)
        {
            if (fromPrintingID == intoPrintingID)
                return SaveResult.Failure(["Pick a different printing to merge into."]);

            var from = await _adminRepository.GetPrintingAsync(fromPrintingID, cancellationToken).ConfigureAwait(false);
            var into = await _adminRepository.GetPrintingAsync(intoPrintingID, cancellationToken).ConfigureAwait(false);
            if (from is null || into is null)
                return SaveResult.Missing("One of the printings no longer exists.");
            if (from.CatalogCardID != into.CatalogCardID)
                return SaveResult.Failure(["Printings can only be merged within the same card."]);
            if (into.Status != CatalogStatus.Active)
                return SaveResult.Failure([$"{CatalogPrintingFactory.Describe(into)} is retired; restore it before merging into it."]);

            CatalogUserRowMoveResult moved = new(0, 0);
            var now = Now();
            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                moved = await _adminRepository.MoveUserRowsAsync(from, into, cancellationToken).ConfigureAwait(false);

                // A hand-added marker would stop the sync noticing the surviving printing disappear, so it isn't carried over.
                foreach (var source in from.Sources.ToList())
                {
                    if (source.Source == CatalogSource.Manual)
                    {
                        _adminRepository.RemovePrintingSource(source);
                        continue;
                    }

                    source.CatalogPrintingID = into.ID;
                    source.DateModified = now;
                }

                into.FirstSeenUTC = into.FirstSeenUTC < from.FirstSeenUTC ? into.FirstSeenUTC : from.FirstSeenUTC;
                into.LastSeenUTC = into.LastSeenUTC > from.LastSeenUTC ? into.LastSeenUTC : from.LastSeenUTC;
                into.DateModified = now;
                from.Status = CatalogStatus.Retired;
                from.DateModified = now;

                await _adminRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Merged catalog printing {FromID} ({From}) into {IntoID} ({Into}): {Moved} rows moved, {Folded} folded",
                from.ID, CatalogPrintingFactory.Describe(from), into.ID, CatalogPrintingFactory.Describe(into), moved.Moved, moved.Folded);
            _cardDataRepository.QueueReload();
            return SaveResult.Success();
        }

        public async Task<PagedResult<CatalogCardRowViewModel>> SearchCardsAsync(CatalogCardSearchCriteria criteria, CancellationToken cancellationToken = default)
        {
            if (criteria is null) throw new ArgumentNullException(nameof(criteria));

            var page = Paging.ClampPage(criteria.Page);
            var pageSize = Paging.ClampPageSize(criteria.PageSize);
            var (cards, totalCount) = await _adminRepository
                .SearchCardsAsync(criteria.Query, criteria.Status, page, pageSize, cancellationToken)
                .ConfigureAwait(false);

            return new PagedResult<CatalogCardRowViewModel>
            {
                Items = cards.Select(c => new CatalogCardRowViewModel
                {
                    HasLockedFields = CatalogLockedFields.Split(c.LockedFields).Count > 0,
                    ID = c.ID,
                    ImageURLSmall = c.Images.OrderBy(i => i.SortOrder).FirstOrDefault()?.ImageURLSmall,
                    KonamiID = c.KonamiID,
                    Name = c.Name,
                    Passcode = c.Passcode,
                    PrintingCount = c.Printings.Count(p => p.Status == CatalogStatus.Active),
                    Status = c.Status
                }).ToList(),
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public async Task<SaveResult> SetCardStatusAsync(int catalogCardID, CatalogStatus status, CancellationToken cancellationToken = default)
        {
            var card = await _adminRepository.GetCardAsync(catalogCardID, cancellationToken).ConfigureAwait(false);
            if (card is null)
                return SaveResult.Missing("That card no longer exists.");
            if (card.Status == status)
                return SaveResult.Success();

            if (status == CatalogStatus.Retired)
            {
                var inUse = (await _adminRepository.CountUserRowsByPrintingAsync(card.ID, cancellationToken).ConfigureAwait(false)).Values.Sum();
                if (inUse > 0)
                    return SaveResult.Failure([$"{inUse} of your collection, wishlist, checked-out, or cart rows still use this card, so it can't be retired."]);
            }

            card.Status = status;
            card.DateModified = Now();
            await _adminRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Catalog card {CardID} set to {Status} by hand", card.ID, status);
            _cardDataRepository.QueueReload();
            return SaveResult.Success();
        }

        public async Task<SaveResult> SetPrintingStatusAsync(int catalogPrintingID, CatalogStatus status, CancellationToken cancellationToken = default)
        {
            var printing = await _adminRepository.GetPrintingAsync(catalogPrintingID, cancellationToken).ConfigureAwait(false);
            if (printing is null)
                return SaveResult.Missing("That printing no longer exists.");
            if (printing.Status == status)
                return SaveResult.Success();

            if (status == CatalogStatus.Retired)
            {
                var inUse = (await _adminRepository.CountUserRowsByPrintingAsync(printing.CatalogCardID, cancellationToken).ConfigureAwait(false))
                    .GetValueOrDefault(printing.ID);
                if (inUse > 0)
                    return SaveResult.Failure([$"{inUse} of your collection, wishlist, checked-out, or cart rows still use {CatalogPrintingFactory.Describe(printing)}; merge it into another printing instead."]);
            }

            printing.Status = status;
            printing.DateModified = Now();
            await _adminRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Catalog printing {PrintingID} ({Printing}) set to {Status} by hand", printing.ID, CatalogPrintingFactory.Describe(printing), status);
            _cardDataRepository.QueueReload();
            return SaveResult.Success();
        }

        public async Task<SaveResult> UnlinkSourceAsync(int printingSourceID, CancellationToken cancellationToken = default)
        {
            var source = await _adminRepository.GetPrintingSourceAsync(printingSourceID, cancellationToken).ConfigureAwait(false);
            if (source is null)
                return SaveResult.Missing("That link no longer exists.");
            if (source.Source != CatalogSource.TCGPlayer)
                return SaveResult.Failure(["Only TCGPlayer product links can be removed."]);

            var now = Now();
            _adminRepository.RemovePrintingSource(source);

            // A product linked from the inbox has an accepted item; reopening it puts the product back in front of the user,
            // because the next sync would otherwise re-detect it under a closed item and say nothing.
            var item = await _adminRepository
                .GetReviewItemAsync(CatalogReviewItemKind.UnmappedTCGProduct, $"tcg:{source.ExternalKey}", cancellationToken)
                .ConfigureAwait(false);
            if (item is not null && item.Status != CatalogReviewItemStatus.Open)
            {
                item.Status = CatalogReviewItemStatus.Open;
                item.Resolution = null;
                item.ResolvedUTC = null;
                item.DateModified = now;
            }

            await _adminRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Unlinked TCGPlayer product {ProductID} from catalog printing {PrintingID}", source.ExternalKey, source.CatalogPrintingID);
            _cardDataRepository.QueueReload();
            return SaveResult.Success();
        }

        public async Task<SaveResult> UnlockFieldAsync(int catalogCardID, string fieldName, CancellationToken cancellationToken = default)
        {
            var field = CatalogCardField.All.FirstOrDefault(f => string.Equals(f, fieldName, StringComparison.OrdinalIgnoreCase));
            if (field is null)
                return SaveResult.Failure([$"\"{fieldName}\" isn't a lockable field."]);

            var card = await _adminRepository.GetCardAsync(catalogCardID, cancellationToken).ConfigureAwait(false);
            if (card is null)
                return SaveResult.Missing("That card no longer exists.");
            if (!card.IsLocked(field))
                return SaveResult.Success();

            card.LockedFields = CatalogLockedFields.Remove(card.LockedFields, field);
            card.DateModified = Now();
            await _adminRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Unlocked {Field} on catalog card {CardID}", field, card.ID);
            return SaveResult.Success();
        }

        public async Task<SaveResult> UpdateCardAsync(int catalogCardID, CatalogCardEditInput input, CancellationToken cancellationToken = default)
        {
            if (input is null) throw new ArgumentNullException(nameof(input));

            var errors = CatalogCardEditor.Validate(input);
            if (errors.Count > 0)
                return SaveResult.Failure(errors);

            var card = await _adminRepository.GetCardAsync(catalogCardID, cancellationToken).ConfigureAwait(false);
            if (card is null)
                return SaveResult.Missing("That card no longer exists.");

            if (input.Passcode is int passcode && passcode != card.Passcode)
            {
                var owner = await _adminRepository.FindOtherCardWithPasscodeAsync(passcode, card.ID, cancellationToken).ConfigureAwait(false);
                if (owner is not null)
                    return SaveResult.Failure([$"Passcode {passcode} already belongs to \"{owner.Name}\"; change that card's passcode first."]);
            }

            var changed = CatalogCardEditor.Apply(card, input, Now());
            if (changed.Count == 0)
                return SaveResult.Success();

            await _adminRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Edited catalog card {CardID} by hand: {Fields}", card.ID, string.Join(", ", changed));
            _cardDataRepository.QueueReload();
            return SaveResult.Success();
        }

        private static CatalogSyncRunViewModel ToViewModel(CatalogSyncRun run) => new()
        {
            CardsAdded = run.CardsAdded,
            CardsUpdated = run.CardsUpdated,
            FinishedUTC = run.FinishedUTC,
            ID = run.ID,
            IsBootstrap = run.IsBootstrap,
            ItemsAutoResolved = run.ItemsAutoResolved,
            ItemsOpened = run.ItemsOpened,
            Message = run.Message,
            PrintingsAdded = run.PrintingsAdded,
            ProductsIgnored = run.ProductsIgnored,
            ProductsLinked = run.ProductsLinked,
            ProductsUnmapped = run.ProductsUnmapped,
            Sources =
            [
                new CatalogSyncSourceResultViewModel("yaml-yugi", run.YamlYugiStatus, run.YamlYugiCardCount),
                new CatalogSyncSourceResultViewModel("YGOProDeck", run.YGOProDeckStatus, run.YGOProDeckCardCount),
                new CatalogSyncSourceResultViewModel("TCGPlayer", run.TCGPlayerStatus, run.TCGPlayerProductCount)
            ],
            StartedUTC = run.StartedUTC,
            Status = run.Status
        };

        private DateTime Now() => _timeProvider.GetUtcNow().UtcDateTime;
    }
}
