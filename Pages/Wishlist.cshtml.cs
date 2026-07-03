using CardCollector.Data.Models;
using CardCollector.Extensions;
using CardCollector.Repository;
using CardCollector.Services;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace CardCollector.Pages
{
    public sealed class WishlistModel : SearchablePageModel
    {
        private readonly ICardService _cardService;
        private readonly ICardSetRepository _cardSetRepository;

        [BindProperty]
        public AcquisitionMethod? AcquisitionMethod { get; set; }

        public IReadOnlyList<string> AvailableRarityNames { get; private set; } = [];

        public IReadOnlyList<string> AvailableSetNames { get; private set; } = [];

        [BindProperty]
        public int CardID { get; set; }

        protected override ICardService CardService => _cardService;

        [BindProperty]
        public CardCondition? Condition { get; set; }

        [BindProperty]
        public CardEdition? Edition { get; set; }

        [BindProperty]
        public int ImageID { get; set; }

        [BindProperty]
        public decimal? MarketPriceAtEntry { get; set; }

        [BindProperty]
        public DateTime? PurchaseDate { get; set; }

        [BindProperty]
        public decimal? PurchasePrice { get; set; }

        [BindProperty]
        public int Quantity { get; set; } = 1;

        public PagedResult<WishlistItemViewModel> Results { get; private set; } = new();

        [BindProperty]
        public string SetCode { get; set; } = string.Empty;

        [BindProperty(SupportsGet = true)]
        public WishlistSortBy SortBy { get; set; } = WishlistSortBy.Name;

        [BindProperty(SupportsGet = true)]
        public bool SortDescending { get; set; } = false;

        public WishlistModel(ICardService cardService, ICardSetRepository cardSetRepository)
        {
            _cardService = cardService;
            _cardSetRepository = cardSetRepository;
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

        public async Task<IActionResult> OnPostOrderAsync()
        {
            await this.WarnIfEditionMismatchAsync(_cardService, CardID, SetCode, RarityName, Edition);

            await _cardService.AddEntryAsync(
                CardID, ImageID, SetCode, CollectionStatus.Ordered,
                Quantity, Condition, Edition,
                AcquisitionMethod,
                PurchaseDate, PurchasePrice, MarketPriceAtEntry, RarityName);

            return RedirectToPage(BuildFilterRedirect());
        }

        public async Task<IActionResult> OnPostOwnAsync()
        {
            await this.WarnIfEditionMismatchAsync(_cardService, CardID, SetCode, RarityName, Edition);

            await _cardService.AddEntryAsync(
                CardID, ImageID, SetCode, CollectionStatus.Owned,
                Quantity, Condition, Edition,
                AcquisitionMethod,
                PurchaseDate, PurchasePrice, MarketPriceAtEntry, RarityName);

            return RedirectToPage(BuildFilterRedirect());
        }

        public async Task<IActionResult> OnPostRemoveAsync(int imageID)
        {
            await _cardService.RemoveFromWishlistAsync(imageID);
            return RedirectToPage(BuildFilterRedirect());
        }

        // Reads filter-state fields from the query string directly because POST body values
        // share names with filter BindProperties and would override the query-string values.
        private object BuildFilterRedirect() => new
        {
            cardType = CardType,
            pageNumber = PageNumber,
            pageSize = PageSize,
            query = Query,
            rarityName = Request.Query["rarityName"].FirstOrDefault(),
            setName = SetName,
            sortBy = SortBy,
            sortDescending = SortDescending
        };
    }
}
