using CardCollector.Data.Models;
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

        public IReadOnlyList<string> AvailableRarityNames { get; private set; } = [];

        public IReadOnlyList<string> AvailableSetNames { get; private set; } = [];

        protected override ICardService CardService => _cardService;

        [BindProperty(SupportsGet = true)]
        public EditionAuditCategory? Category { get; set; }

        public PagedResult<EditionAuditGroupViewModel> Results { get; private set; } = new();

        public override int ActiveFilterCount => base.ActiveFilterCount + (Category.HasValue ? 1 : 0);

        public override bool HasActiveFilters => base.HasActiveFilters || Category.HasValue;

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

        public string GetFilterParams()
        {
            var rarityName = Request.Query["rarityName"].FirstOrDefault();
            return $"cardType={Uri.EscapeDataString(CardType ?? string.Empty)}&setName={Uri.EscapeDataString(SetName ?? string.Empty)}&rarityName={Uri.EscapeDataString(rarityName ?? string.Empty)}&category={Category}&pageNumber={PageNumber}&pageSize={PageSize}&query={Uri.EscapeDataString(Query ?? string.Empty)}";
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

            var setCodes = await _collectionRepository.GetDistinctSetCodesAsync().ConfigureAwait(false);
            var setNamesByCode = _cardDataRepository.GetSetNamesByCode();
            AvailableSetNames = setCodes
                .Select(c => setNamesByCode.TryGetValue(c, out var n) ? n : c)
                .Distinct()
                .OrderBy(n => n)
                .ToList();

            AvailableRarityNames = await _collectionRepository.GetDistinctRarityNamesAsync().ConfigureAwait(false);

            Results = await _cardService.SearchEditionAuditAsync(BuildCurrentCriteria(PageNumber, PageSize)).ConfigureAwait(false);
        }

        public async Task<IActionResult> OnPostEditAsync(
            int entryID, int quantity,
            CardCondition? condition, CardEdition? edition,
            AcquisitionMethod? acquisitionMethod,
            DateTime? purchaseDate, decimal? purchasePrice,
            decimal? marketPriceAtEntry, string? rarityName = null)
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
                Quantity = quantity < 1 ? 1 : quantity,
                RarityName = string.IsNullOrWhiteSpace(rarityName) ? null : rarityName
            };

            await _collectionRepository.UpdateAsync(entry);

            if (!IsAjaxRequest())
                return RedirectToPage(BuildFilterRedirect());

            var groups = await _cardService.SearchEditionAuditAsync(BuildCurrentCriteria(1, int.MaxValue)).ConfigureAwait(false);
            Response.Headers["X-Total-Count"] = groups.TotalCount.ToString();

            var match = existing is null
                ? null
                : groups.Items.FirstOrDefault(g =>
                    g.CardID == existing.CardID && g.SetCode.Equals(existing.SetCode, StringComparison.OrdinalIgnoreCase));

            if (match is null)
                return Content(string.Empty, "text/html");

            var html = await _razorPartialRenderer.RenderPartialAsync(this, "_EditionAuditGroupRow", new EditionAuditGroupRowViewModel
            {
                FilterParams = GetFilterParams(),
                Group = match
            }).ConfigureAwait(false);

            return Content(html, "text/html");
        }

        private object BuildFilterRedirect() => new
        {
            cardType = CardType,
            category = Category?.ToString(),
            pageNumber = PageNumber,
            pageSize = PageSize,
            query = Query,
            rarityName = Request.Query["rarityName"].FirstOrDefault(),
            setName = SetName
        };

        private EditionAuditSearchCriteria BuildCurrentCriteria(int page, int pageSize) => new()
        {
            CardType = CardType,
            Category = Category,
            Page = page,
            PageSize = pageSize,
            Query = Query,
            RarityName = Request.Query["rarityName"].FirstOrDefault(),
            SetName = SetName
        };

        private bool IsAjaxRequest() =>
            Request.Headers["X-Requested-With"] == "XMLHttpRequest";
    }
}
