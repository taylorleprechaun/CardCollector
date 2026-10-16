using CardCollector.Data.Models;
using CardCollector.Extensions;
using CardCollector.Repository;
using CardCollector.Services;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

namespace CardCollector.Pages
{
    public sealed class WishlistModel : SearchablePageModel
    {
        private readonly ICardService _cardService;
        private readonly ICardSetRepository _cardSetRepository;
        private readonly IRazorPartialRenderer _razorPartialRenderer;

        [BindProperty]
        public AcquisitionMethod? AcquisitionMethod { get; set; }

        public IReadOnlyList<string> AvailableRarityNames { get; private set; } = [];

        public IReadOnlyList<string> AvailableSetNames { get; private set; } = [];

        protected override ICardService CardService => _cardService;

        [BindProperty]
        public CardCondition? Condition { get; set; }

        [BindProperty]
        public CardEdition? Edition { get; set; }

        [BindProperty]
        public decimal? MarketPriceAtEntry { get; set; }

        [BindProperty]
        public int PreferredVersionID { get; set; }

        [BindProperty]
        public int PrintingID { get; set; }

        [BindProperty]
        public DateTime? PurchaseDate { get; set; }

        [BindProperty]
        public decimal? PurchasePrice { get; set; }

        [BindProperty]
        public int Quantity { get; set; } = 1;

        public PagedResult<WishlistItemViewModel> Results { get; private set; } = new();

        [BindProperty(SupportsGet = true)]
        public WishlistSortBy SortBy { get; set; } = WishlistSortBy.Name;

        [BindProperty(SupportsGet = true)]
        public bool SortDescending { get; set; } = false;

        public WishlistModel(ICardService cardService, ICardSetRepository cardSetRepository, IRazorPartialRenderer razorPartialRenderer)
        {
            _cardService = cardService;
            _cardSetRepository = cardSetRepository;
            _razorPartialRenderer = razorPartialRenderer;
        }

        public string GetFilterParams()
        {
            return $"cardType={Uri.EscapeDataString(CardType ?? string.Empty)}&setName={Uri.EscapeDataString(SetName ?? string.Empty)}&rarityName={Uri.EscapeDataString(RarityName ?? string.Empty)}&sortBy={SortBy}&sortDescending={SortDescending}&pageNumber={PageNumber}&pageSize={PageSize}&query={Uri.EscapeDataString(Query ?? string.Empty)}";
        }

        public override IReadOnlyDictionary<string, string?> GetPaginationParams()
        {
            var dict = new Dictionary<string, string?>(base.GetPaginationParams())
            {
                ["sortBy"] = SortBy.ToString(),
                ["sortDescending"] = SortDescending.ToString()
            };
            return dict;
        }

        public string GetTCGDate(string setCode) =>
            _cardSetRepository.GetTCGDateBySetCode(setCode) ?? string.Empty;

        public async Task OnGetAsync()
        {
            NormalizeSearchParameters();

            AvailableSetNames = await _cardService.GetWishlistDistinctSetNamesAsync().ConfigureAwait(false);
            AvailableRarityNames = await _cardService.GetWishlistDistinctRarityNamesAsync().ConfigureAwait(false);

            var criteria = new WishlistSearchCriteria
            {
                CardType = CardType,
                Page = PageNumber,
                PageSize = PageSize,
                Query = Query,
                RarityName = RarityName,
                SetName = SetName,
                SortBy = SortBy,
                SortDescending = SortDescending
            };

            var result = await _cardService.SearchWishlistAsync(criteria).ConfigureAwait(false);
            Results = result.PagedItems;
        }

        public async Task<IActionResult> OnPostAddToCartAsync(int printingID, int quantity, decimal? marketPrice, int preferredVersionID)
        {
            if (_cardService.GetPrintingByID(printingID) is null)
                return BadRequest();

            await _cardService.AddToCartAsync(printingID, quantity, marketPrice).ConfigureAwait(false);

            return await RespondAfterMutationAsync(preferredVersionID).ConfigureAwait(false);
        }

        public async Task<IActionResult> OnPostOwnAsync()
        {
            await this.WarnIfEditionMismatchAsync(_cardService, PrintingID, Edition);

            await _cardService.AddEntryAsync(
                PrintingID, CollectionStatus.Owned,
                Quantity, Condition, Edition,
                AcquisitionMethod,
                PurchaseDate, PurchasePrice, MarketPriceAtEntry);

            return await RespondAfterMutationAsync(PreferredVersionID).ConfigureAwait(false);
        }

        public async Task<IActionResult> OnPostRemoveAsync(int preferredVersionID)
        {
            await _cardService.RemoveFromWishlistAsync(preferredVersionID);
            return await RespondAfterMutationAsync(preferredVersionID).ConfigureAwait(false);
        }

        private WishlistSearchCriteria BuildCurrentCriteria(int page, int pageSize)
        {
            return new WishlistSearchCriteria
            {
                CardType = CardType,
                Page = page,
                PageSize = pageSize,
                Query = Query,
                RarityName = RarityName,
                SetName = SetName,
                SortBy = SortBy,
                SortDescending = SortDescending
            };
        }

        // Reads filter-state fields from the query string directly because POST body values
        // share names with filter BindProperties and would override the query-string values.
        private object BuildFilterRedirect() => new
        {
            cardType = CardType,
            pageNumber = PageNumber,
            pageSize = PageSize,
            query = Query,
            rarityName = RarityName,
            setName = SetName,
            sortBy = SortBy,
            sortDescending = SortDescending
        };

        private async Task<IActionResult> RespondAfterMutationAsync(int preferredVersionID)
        {
            if (!Request.IsAjaxRequest())
                return RedirectToPage(BuildFilterRedirect());

            var result = await _cardService.SearchWishlistAsync(BuildCurrentCriteria(1, int.MaxValue)).ConfigureAwait(false);
            Response.Headers["X-Total-Count"] = result.PagedItems.TotalCount.ToString();

            var (cartCount, cartTotal) = await _cardService.GetCartSummaryAsync().ConfigureAwait(false);
            Response.Headers["X-Cart-Count"] = cartCount.ToString(CultureInfo.InvariantCulture);
            Response.Headers["X-Cart-Total"] = cartTotal.ToString(CultureInfo.InvariantCulture);

            var match = result.PagedItems.Items.FirstOrDefault(i => i.PreferredVersionID == preferredVersionID);
            if (match is null)
                return Content(string.Empty, "text/html");

            var html = await _razorPartialRenderer.RenderPartialAsync(this, "_WishlistRow", new WishlistRowViewModel
            {
                FilterParams = GetFilterParams(),
                Item = match,
                TCGDate = GetTCGDate(match.SetCode)
            }).ConfigureAwait(false);

            return Content(html, "text/html");
        }
    }
}
