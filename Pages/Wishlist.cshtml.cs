using CardCollector.Data.Models;
using CardCollector.Services;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CardCollector.Pages
{
    public class WishlistModel : PageModel
    {
        private readonly ICardService _cardService;

        private static readonly int[] ValidPageSizes = [10, 25, 50, 100];

        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;

        [BindProperty(SupportsGet = true)]
        public int PageSize { get; set; } = 25;

        [BindProperty(SupportsGet = true)]
        public string? Query { get; set; }

        [BindProperty]
        public AcquisitionMethod? SelectedAcquisitionMethod { get; set; }

        [BindProperty]
        public int CardID { get; set; }

        [BindProperty]
        public CardCondition? SelectedCondition { get; set; }

        [BindProperty]
        public CardEdition? SelectedEdition { get; set; }

        [BindProperty]
        public int ImageID { get; set; }

        [BindProperty]
        public DateTime? PurchaseDate { get; set; }

        [BindProperty]
        public decimal? PurchasePrice { get; set; }

        [BindProperty]
        public int Quantity { get; set; } = 1;

        [BindProperty]
        public string SetCode { get; set; } = string.Empty;

        public PagedResult<WishlistItemViewModel> Results { get; private set; } = new();

        public WishlistModel(ICardService cardService)
        {
            _cardService = cardService;
        }

        public async Task OnGetAsync()
        {
            if (PageNumber < 1)
                PageNumber = 1;

            if (!ValidPageSizes.Contains(PageSize))
                PageSize = 25;

            Results = await _cardService.SearchWishlistAsync(Query, PageNumber, PageSize);
        }

        public async Task<IActionResult> OnPostOrderAsync()
        {
            await _cardService.AddEntryAsync(
                CardID, ImageID, SetCode, CollectionStatus.Ordered,
                Quantity, SelectedCondition, SelectedEdition,
                SelectedAcquisitionMethod, false,
                PurchaseDate, PurchasePrice);
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostOwnAsync()
        {
            await _cardService.AddEntryAsync(
                CardID, ImageID, SetCode, CollectionStatus.Owned,
                Quantity, SelectedCondition, SelectedEdition,
                SelectedAcquisitionMethod, false,
                PurchaseDate, PurchasePrice);
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRemoveAsync(int imageID)
        {
            await _cardService.RemoveFromWishlistAsync(imageID);
            return RedirectToPage();
        }
    }
}
