using CardCollector.Data.Models;
using CardCollector.Extensions;
using CardCollector.Repository;
using CardCollector.Services;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace CardCollector.Pages
{
    public sealed class CollectionModel : SearchablePageModel
    {
        private readonly ICardDataRepository _cardDataRepository;
        private readonly ICardService _cardService;
        private readonly ICardSetRepository _cardSetRepository;
        private readonly ICollectionRepository _collectionRepository;
        private readonly IRazorPartialRenderer _razorPartialRenderer;

        [BindProperty(SupportsGet = true)]
        public AcquisitionMethod? AcquisitionMethod { get; set; }

        public IReadOnlyList<AcquisitionMethod> AvailableAcquisitionMethods { get; private set; } = [];

        public IReadOnlyList<CardCondition> AvailableConditions { get; private set; } = [];

        public IReadOnlyList<CardEdition> AvailableEditions { get; private set; } = [];

        public IReadOnlyList<string> AvailableRarityNames { get; private set; } = [];

        public IReadOnlyList<string> AvailableSetNames { get; private set; } = [];

        protected override ICardService CardService => _cardService;

        [BindProperty(SupportsGet = true)]
        public string? CheckedOutFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public CardCondition? Condition { get; set; }

        [BindProperty(SupportsGet = true)]
        public CardEdition? Edition { get; set; }

        public PagedResult<CollectionGroupViewModel> GroupedCards { get; private set; } = new();

        public override int ActiveFilterCount => base.ActiveFilterCount
            + (Condition.HasValue ? 1 : 0)
            + (Edition.HasValue ? 1 : 0)
            + (AcquisitionMethod.HasValue ? 1 : 0)
            + (string.IsNullOrEmpty(CheckedOutFilter) ? 0 : 1);

        public override bool HasActiveFilters => base.HasActiveFilters
            || Condition.HasValue
            || Edition.HasValue
            || AcquisitionMethod.HasValue
            || !string.IsNullOrEmpty(CheckedOutFilter);

        public CollectionModel(
            ICardDataRepository cardDataRepository,
            ICardService cardService,
            ICardSetRepository cardSetRepository,
            ICollectionRepository collectionRepository,
            IRazorPartialRenderer razorPartialRenderer)
        {
            _cardDataRepository = cardDataRepository;
            _cardService = cardService;
            _cardSetRepository = cardSetRepository;
            _collectionRepository = collectionRepository;
            _razorPartialRenderer = razorPartialRenderer;
        }

        public string GetFilterParams()
        {
            var (acquisitionMethod, checkedOutFilter, condition, edition, rarityName) = GetSafeFilterQueryValues();
            return $"cardType={Uri.EscapeDataString(CardType ?? string.Empty)}&setName={Uri.EscapeDataString(SetName ?? string.Empty)}&rarityName={Uri.EscapeDataString(rarityName ?? string.Empty)}&condition={condition}&edition={edition}&acquisitionMethod={acquisitionMethod}&checkedOutFilter={checkedOutFilter}&pageNumber={PageNumber}&pageSize={PageSize}&query={Uri.EscapeDataString(Query ?? string.Empty)}";
        }

        public override IReadOnlyDictionary<string, string?> GetPaginationParams()
        {
            var dict = new Dictionary<string, string?>(base.GetPaginationParams())
            {
                ["acquisitionMethod"] = AcquisitionMethod?.ToString(),
                ["checkedOutFilter"] = CheckedOutFilter,
                ["condition"] = Condition?.ToString(),
                ["edition"] = Edition?.ToString()
            };
            return dict;
        }

        public string GetTCGDate(string setCode) =>
            _cardSetRepository.GetTCGDateBySetCode(setCode) ?? string.Empty;

        public async Task OnGetAsync()
        {
            NormalizeSearchParameters();

            var (setCodes, rarityNames) = await _cardService.GetOwnedSetCodesAndRarityNamesAsync().ConfigureAwait(false);
            var setNamesByCode = _cardDataRepository.GetSetNamesByCode();
            AvailableSetNames = setCodes
                .Select(c => setNamesByCode.TryGetValue(c, out var n) ? n : c)
                .Distinct()
                .OrderBy(n => n)
                .ToList();

            AvailableRarityNames = rarityNames;
            AvailableConditions = await _collectionRepository.GetDistinctConditionsAsync().ConfigureAwait(false);
            AvailableEditions = await _collectionRepository.GetDistinctEditionsAsync().ConfigureAwait(false);
            AvailableAcquisitionMethods = await _collectionRepository.GetDistinctAcquisitionMethodsAsync().ConfigureAwait(false);

            GroupedCards = await _cardService.SearchGroupedOwnedAsync(BuildCurrentCriteria(PageNumber, PageSize)).ConfigureAwait(false);
        }

        public async Task<IActionResult> OnPostAddPurchaseAsync(
            int printingID,
            int quantity,
            CardCondition? condition, CardEdition? edition,
            AcquisitionMethod? acquisitionMethod,
            DateTime? purchaseDate, decimal? purchasePrice, decimal? marketPriceAtEntry,
            bool setAsPreferred = false)
        {
            await this.WarnIfEditionMismatchAsync(_cardService, printingID, edition);

            await _cardService.AddEntryAsync(
                printingID, CollectionStatus.Owned,
                quantity, condition, edition,
                acquisitionMethod,
                purchaseDate, purchasePrice, marketPriceAtEntry);

            if (setAsPreferred)
                await _cardService.SavePreferredVersionAsync(printingID);

            return await RespondAfterMutationAsync(printingID).ConfigureAwait(false);
        }

        public async Task<IActionResult> OnPostCheckInAsync(int printingID)
        {
            await _cardService.CheckInCardAsync(printingID).ConfigureAwait(false);
            return await RespondAfterMutationAsync(printingID).ConfigureAwait(false);
        }

        public async Task<IActionResult> OnPostCheckOutAsync(int printingID, int quantity)
        {
            if (quantity >= 1)
                await _cardService.CheckOutCardAsync(printingID, quantity).ConfigureAwait(false);
            return await RespondAfterMutationAsync(printingID).ConfigureAwait(false);
        }

        public async Task<IActionResult> OnPostDeleteAsync(int entryID)
        {
            var existing = await _collectionRepository.GetByIDAsync(entryID).ConfigureAwait(false);

            await _collectionRepository.DeleteAsync(entryID);

            return await RespondAfterMutationAsync(existing?.CatalogPrintingID ?? 0).ConfigureAwait(false);
        }

        public async Task<IActionResult> OnPostEditAsync(
            int entryID, int quantity,
            CardCondition? condition, CardEdition? edition,
            AcquisitionMethod? acquisitionMethod,
            DateTime? purchaseDate, decimal? purchasePrice,
            decimal? marketPriceAtEntry)
        {
            var existing = await _collectionRepository.GetByIDAsync(entryID).ConfigureAwait(false);

            var entry = new CollectionEntry
            {
                ID = entryID,
                AcquisitionMethod = acquisitionMethod,
                Condition = condition,
                Edition = edition,
                MarketPriceAtEntry = marketPriceAtEntry,
                PurchaseDate = purchaseDate,
                PurchasePrice = purchasePrice,
                Quantity = quantity < 1 ? 1 : quantity
            };

            await _collectionRepository.UpdateAsync(entry);

            return await RespondAfterMutationAsync(existing?.CatalogPrintingID ?? 0).ConfigureAwait(false);
        }

        private CollectionSearchCriteria BuildCurrentCriteria(int page, int pageSize)
        {
            var (acquisitionMethodRaw, checkedOutFilter, conditionRaw, editionRaw, rarityName) = GetSafeFilterQueryValues();

            return new CollectionSearchCriteria
            {
                AcquisitionMethod = Enum.TryParse<AcquisitionMethod>(acquisitionMethodRaw, out var acquisitionMethod) ? acquisitionMethod : null,
                CardType = CardType,
                Condition = Enum.TryParse<CardCondition>(conditionRaw, out var condition) ? condition : null,
                Edition = Enum.TryParse<CardEdition>(editionRaw, out var edition) ? edition : null,
                IsCheckedOut = ParseFilter(checkedOutFilter),
                Page = page,
                PageSize = pageSize,
                Query = Query,
                RarityName = rarityName,
                SetName = SetName
            };
        }

        private object BuildFilterRedirect()
        {
            var (acquisitionMethod, checkedOutFilter, condition, edition, rarityName) = GetSafeFilterQueryValues();
            return new
            {
                acquisitionMethod,
                cardType = CardType,
                checkedOutFilter,
                condition,
                edition,
                pageNumber = PageNumber,
                pageSize = PageSize,
                query = Query,
                rarityName,
                setName = SetName
            };
        }

        private (string? AcquisitionMethod, string? CheckedOutFilter, string? Condition, string? Edition, string? RarityName) GetSafeFilterQueryValues() => (
            Request.Query["acquisitionMethod"].FirstOrDefault(),
            Request.Query["checkedOutFilter"].FirstOrDefault(),
            Request.Query["condition"].FirstOrDefault(),
            Request.Query["edition"].FirstOrDefault(),
            Request.Query["rarityName"].FirstOrDefault());

        private static bool? ParseFilter(string? value) => value == "yes" ? true : value == "no" ? false : null;

        /// <summary>
        /// After a change: a full-page post redirects back; an AJAX post gets the printing's row re-rendered, or nothing
        /// when the printing no longer has a row under the current filters.
        /// </summary>
        private async Task<IActionResult> RespondAfterMutationAsync(int printingID)
        {
            if (!Request.IsAjaxRequest())
                return RedirectToPage(BuildFilterRedirect());

            var groups = await _cardService.SearchGroupedOwnedAsync(BuildCurrentCriteria(1, int.MaxValue)).ConfigureAwait(false);
            Response.Headers["X-Total-Count"] = groups.TotalCount.ToString();

            var match = printingID > 0 ? groups.Items.FirstOrDefault(g => g.PrintingID == printingID) : null;
            if (match is null)
                return Content(string.Empty, "text/html");

            var html = await _razorPartialRenderer.RenderPartialAsync(this, "_CollectionGroupRow", new CollectionGroupRowViewModel
            {
                FilterParams = GetFilterParams(),
                Group = match,
                TCGDate = GetTCGDate(match.SetCode)
            }).ConfigureAwait(false);

            return Content(html, "text/html");
        }
    }
}
