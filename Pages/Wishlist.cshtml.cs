using CardCollector.Data.Models;
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

        [BindProperty]
        public int CardID { get; set; }

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

        [BindProperty]
        public string? RarityName { get; set; }

        public PagedResult<WishlistItemViewModel> Results { get; private set; } = new();

        [BindProperty]
        public string SetCode { get; set; } = string.Empty;

        [BindProperty(SupportsGet = true)]
        public WishlistSortBy SortBy { get; set; } = WishlistSortBy.Name;

        [BindProperty(SupportsGet = true)]
        public bool SortDescending { get; set; } = false;

        protected override ICardService CardService => _cardService;

        public WishlistModel(ICardService cardService, ICardSetRepository cardSetRepository)
        {
            _cardService = cardService;
            _cardSetRepository = cardSetRepository;
        }

        public IDictionary<string, string?> GetPaginationParams() => new Dictionary<string, string?>
        {
            ["sortBy"] = SortBy.ToString(),
            ["sortDescending"] = SortDescending.ToString()
        };

        public string GetTCGDate(string setCode) =>
            _cardSetRepository.GetTCGDateBySetCode(setCode) ?? string.Empty;

        public async Task OnGetAsync()
        {
            NormalizeSearchParameters();
            var result = await _cardService.SearchWishlistAsync(Query, PageNumber, PageSize, SortBy, SortDescending);
            Results = result.PagedItems;
        }

        public async Task<IActionResult> OnPostOrderAsync()
        {
            var added = await _cardService.AddEntryAsync(
                CardID, ImageID, SetCode, CollectionStatus.Ordered,
                Quantity, Condition, Edition,
                AcquisitionMethod, false,
                PurchaseDate, PurchasePrice, MarketPriceAtEntry, RarityName);

            if (!added)
                TempData["Error"] = "That printing is already in your collection.";

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostOwnAsync()
        {
            var added = await _cardService.AddEntryAsync(
                CardID, ImageID, SetCode, CollectionStatus.Owned,
                Quantity, Condition, Edition,
                AcquisitionMethod, false,
                PurchaseDate, PurchasePrice, MarketPriceAtEntry, RarityName);

            if (!added)
                TempData["Error"] = "That printing is already in your collection.";

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRemoveAsync(int imageID)
        {
            await _cardService.RemoveFromWishlistAsync(imageID);
            return RedirectToPage();
        }
    }
}
