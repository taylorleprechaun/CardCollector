using CardCollector.Data.Models;
using CardCollector.Services;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace CardCollector.Pages
{
    public sealed class WishlistModel : SearchablePageModel
    {
        private readonly ICardService _cardService;

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

        [BindProperty]
        public string SetCode { get; set; } = string.Empty;

        public PagedResult<WishlistItemViewModel> Results { get; private set; } = new();

        [BindProperty(SupportsGet = true)]
        public WishlistSortBy SortBy { get; set; } = WishlistSortBy.Name;

        [BindProperty(SupportsGet = true)]
        public bool SortDescending { get; set; } = false;

        public WishlistModel(ICardService cardService)
        {
            _cardService = cardService;
        }

        public async Task OnGetAsync()
        {
            NormalizeSearchParameters();
            var result = await _cardService.SearchWishlistAsync(Query, PageNumber, PageSize, SortBy, SortDescending);
            Results = result.PagedItems;
        }

        public IActionResult OnGetAutocomplete(string? q)
        {
            if (string.IsNullOrWhiteSpace(q))
                return new JsonResult(Array.Empty<string>());

            return new JsonResult(_cardService.GetCardNameSuggestions(q));
        }

        public async Task<IActionResult> OnPostOrderAsync()
        {
            await _cardService.AddEntryAsync(
                CardID, ImageID, SetCode, CollectionStatus.Ordered,
                Quantity, Condition, Edition,
                AcquisitionMethod, false,
                PurchaseDate, PurchasePrice, MarketPriceAtEntry, RarityName);
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostOwnAsync()
        {
            await _cardService.AddEntryAsync(
                CardID, ImageID, SetCode, CollectionStatus.Owned,
                Quantity, Condition, Edition,
                AcquisitionMethod, false,
                PurchaseDate, PurchasePrice, MarketPriceAtEntry, RarityName);
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRemoveAsync(int imageID)
        {
            await _cardService.RemoveFromWishlistAsync(imageID);
            return RedirectToPage();
        }

        public Dictionary<string, string?> GetPaginationParams() => new()
        {
            ["sortBy"] = SortBy.ToString(),
            ["sortDescending"] = SortDescending.ToString()
        };
    }
}
