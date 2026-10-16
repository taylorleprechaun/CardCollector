using CardCollector.Data.Models;
using CardCollector.Extensions;
using CardCollector.Repository;
using CardCollector.Services;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace CardCollector.Pages
{
    public sealed class EditionAuditModel : SearchablePageModel
    {
        private readonly ICardDataRepository _cardDataRepository;
        private readonly ICardService _cardService;
        private readonly ICollectionRepository _collectionRepository;
        private readonly IRazorPartialRenderer _razorPartialRenderer;

        public EditionAuditModel(
            ICardDataRepository cardDataRepository,
            ICardService cardService,
            ICollectionRepository collectionRepository,
            IRazorPartialRenderer razorPartialRenderer)
        {
            _cardDataRepository = cardDataRepository;
            _cardService = cardService;
            _collectionRepository = collectionRepository;
            _razorPartialRenderer = razorPartialRenderer;
        }

        public override int ActiveFilterCount => base.ActiveFilterCount + (Category.HasValue ? 1 : 0);
        public IReadOnlyList<string> AvailableRarityNames { get; private set; } = [];

        public IReadOnlyList<string> AvailableSetNames { get; private set; } = [];

        [BindProperty(SupportsGet = true)]
        public EditionAuditCategory? Category { get; set; }

        public override bool HasActiveFilters => base.HasActiveFilters || Category.HasValue;
        public PagedResult<EditionAuditGroupViewModel> Results { get; private set; } = new();
        protected override ICardService CardService => _cardService;

        public string GetFilterParams()
        {
            return $"cardType={Uri.EscapeDataString(CardType ?? string.Empty)}&setName={Uri.EscapeDataString(SetName ?? string.Empty)}&rarityName={Uri.EscapeDataString(RarityName ?? string.Empty)}&category={Category}&pageNumber={PageNumber}&pageSize={PageSize}&query={Uri.EscapeDataString(Query ?? string.Empty)}";
        }

        public override IReadOnlyDictionary<string, string?> GetPaginationParams()
        {
            var dict = new Dictionary<string, string?>(base.GetPaginationParams())
            {
                ["category"] = Category?.ToString()
            };
            return dict;
        }

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

            Results = await _cardService.SearchEditionAuditAsync(BuildCurrentCriteria(PageNumber, PageSize)).ConfigureAwait(false);
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

            if (!Request.IsAjaxRequest())
                return RedirectToPage(BuildFilterRedirect());

            var groups = await _cardService.SearchEditionAuditAsync(BuildCurrentCriteria(1, int.MaxValue)).ConfigureAwait(false);
            Response.Headers["X-Total-Count"] = groups.TotalCount.ToString();

            var match = existing?.CatalogPrintingID is int printingID
                ? groups.Items.FirstOrDefault(g => g.PrintingID == printingID)
                : null;

            if (match is null)
                return Content(string.Empty, "text/html");

            var html = await _razorPartialRenderer.RenderPartialAsync(this, "_EditionAuditGroupRow", new EditionAuditGroupRowViewModel
            {
                FilterParams = GetFilterParams(),
                Group = match
            }).ConfigureAwait(false);

            return Content(html, "text/html");
        }

        private EditionAuditSearchCriteria BuildCurrentCriteria(int page, int pageSize) => new()
        {
            CardType = CardType,
            Category = Category,
            Page = page,
            PageSize = pageSize,
            Query = Query,
            RarityName = RarityName,
            SetName = SetName
        };

        private object BuildFilterRedirect() => new
        {
            cardType = CardType,
            category = Category?.ToString(),
            pageNumber = PageNumber,
            pageSize = PageSize,
            query = Query,
            rarityName = RarityName,
            setName = SetName
        };
    }
}
