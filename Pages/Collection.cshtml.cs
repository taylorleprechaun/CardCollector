using CardCollector.Data.Models;
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
            ICollectionRepository collectionRepository)
        {
            _cardDataRepository = cardDataRepository;
            _cardService = cardService;
            _cardSetRepository = cardSetRepository;
            _collectionRepository = collectionRepository;
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

            var setCodes = await _collectionRepository.GetDistinctSetCodesAsync().ConfigureAwait(false);
            var setNamesByCode = _cardDataRepository.GetSetNamesByCode();
            AvailableSetNames = setCodes
                .Select(c => setNamesByCode.TryGetValue(c, out var n) ? n : c)
                .Distinct()
                .OrderBy(n => n)
                .ToList();

            AvailableRarityNames = await _collectionRepository.GetDistinctRarityNamesAsync().ConfigureAwait(false);
            AvailableConditions = await _collectionRepository.GetDistinctConditionsAsync().ConfigureAwait(false);
            AvailableEditions = await _collectionRepository.GetDistinctEditionsAsync().ConfigureAwait(false);
            AvailableAcquisitionMethods = await _collectionRepository.GetDistinctAcquisitionMethodsAsync().ConfigureAwait(false);

            var criteria = new CollectionSearchCriteria
            {
                AcquisitionMethod = AcquisitionMethod,
                CardType = CardType,
                Condition = Condition,
                Edition = Edition,
                IsCheckedOut = ParseFilter(CheckedOutFilter),
                Page = PageNumber,
                PageSize = PageSize,
                Query = Query,
                RarityName = RarityName,
                SetName = SetName
            };

            GroupedCards = await _cardService.SearchGroupedOwnedAsync(criteria).ConfigureAwait(false);
        }

        public async Task<IActionResult> OnPostCheckInAsync(int imageID, string setCode)
        {
            await _cardService.CheckInCardAsync(imageID, setCode).ConfigureAwait(false);
            return RedirectToPage(BuildFilterRedirect());
        }

        public async Task<IActionResult> OnPostCheckOutAsync(int cardID, int imageID, string setCode, int quantity)
        {
            if (quantity >= 1)
                await _cardService.CheckOutCardAsync(cardID, imageID, setCode, quantity).ConfigureAwait(false);
            return RedirectToPage(BuildFilterRedirect());
        }

        public async Task<IActionResult> OnPostAddPurchaseAsync(
            int cardID, int imageID, string setCode,
            int quantity,
            CardCondition? condition, CardEdition? edition,
            AcquisitionMethod? acquisitionMethod,
            DateTime? purchaseDate, decimal? purchasePrice, decimal? marketPriceAtEntry,
            bool setAsPreferred = false,
            string? rarityName = null)
        {
            await _cardService.AddEntryAsync(
                cardID, imageID, setCode, CollectionStatus.Owned,
                quantity, condition, edition,
                acquisitionMethod,
                purchaseDate, purchasePrice, marketPriceAtEntry, rarityName);

            if (setAsPreferred)
                await _cardService.SavePreferredVersionAsync(cardID, imageID, setCode, rarityName);

            return RedirectToPage(BuildFilterRedirect());
        }

        public async Task<IActionResult> OnPostDeleteAsync(int entryID)
        {
            await _collectionRepository.DeleteAsync(entryID);
            return RedirectToPage(BuildFilterRedirect());
        }

        public async Task<IActionResult> OnPostEditAsync(
            int entryID, int quantity,
            CardCondition? condition, CardEdition? edition,
            AcquisitionMethod? acquisitionMethod,
            DateTime? purchaseDate, decimal? purchasePrice,
            string? rarityName = null)
        {
            var entry = new CollectionEntry
            {
                ID = entryID,
                AcquisitionMethod = acquisitionMethod,
                Condition = condition,
                Edition = edition,
                PurchaseDate = purchaseDate,
                PurchasePrice = purchasePrice,
                Quantity = quantity < 1 ? 1 : quantity,
                RarityName = string.IsNullOrWhiteSpace(rarityName) ? null : rarityName
            };

            await _collectionRepository.UpdateAsync(entry);
            return RedirectToPage(BuildFilterRedirect());
        }

        // Builds redirect params reading filter-state fields from the query string directly,
        // because POST body values (condition, edition, etc.) share names with filter BindProperties
        // and would override the query-string filter values if read from `this`.
        private object BuildFilterRedirect() => new
        {
            acquisitionMethod = Request.Query["acquisitionMethod"].FirstOrDefault(),
            cardType = CardType,
            checkedOutFilter = Request.Query["checkedOutFilter"].FirstOrDefault(),
            condition = Request.Query["condition"].FirstOrDefault(),
            edition = Request.Query["edition"].FirstOrDefault(),
            pageNumber = PageNumber,
            pageSize = PageSize,
            query = Query,
            rarityName = Request.Query["rarityName"].FirstOrDefault(),
            setName = SetName
        };

        private static bool? ParseFilter(string? value) =>
            value == "yes" ? true : value == "no" ? false : null;
    }
}
