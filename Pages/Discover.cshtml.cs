using CardCollector.Data.Models;
using CardCollector.DTO;
using CardCollector.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CardCollector.Pages
{
    public class DiscoverModel : PageModel
    {
        private readonly ICardService _cardService;

        [BindProperty]
        public int CardID { get; set; }

        public Card? CurrentCard { get; private set; }

        public Image? CurrentImage { get; private set; }

        [BindProperty]
        public int ImageID { get; set; }

        public bool IsComplete { get; private set; }

        [BindProperty]
        public bool IsPlaceholder { get; set; }

        [BindProperty]
        public DateTime? PurchaseDate { get; set; }

        [BindProperty]
        public decimal? PurchasePrice { get; set; }

        [BindProperty]
        public int Quantity { get; set; } = 1;

        [BindProperty]
        public AcquisitionMethod? SelectedAcquisitionMethod { get; set; }

        [BindProperty]
        public CardCondition? SelectedCondition { get; set; }

        [BindProperty]
        public CardEdition? SelectedEdition { get; set; }

        [BindProperty]
        public string SetCode { get; set; } = string.Empty;

        public DiscoverModel(ICardService cardService)
        {
            _cardService = cardService;
        }

        public async Task OnGetAsync()
        {
            var result = await _cardService.GetRandomUncollectedAsync();
            if (result is null)
            {
                IsComplete = true;
                return;
            }

            CurrentCard = result.Value.Card;
            CurrentImage = result.Value.Image;
        }

        public async Task<IActionResult> OnPostOrderAsync()
        {
            await _cardService.AddEntryAsync(
                CardID, ImageID, SetCode, CollectionStatus.Ordered,
                Quantity, SelectedCondition, SelectedEdition,
                SelectedAcquisitionMethod, IsPlaceholder,
                PurchaseDate, PurchasePrice);
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostOwnAsync()
        {
            await _cardService.AddEntryAsync(
                CardID, ImageID, SetCode, CollectionStatus.Owned,
                Quantity, SelectedCondition, SelectedEdition,
                SelectedAcquisitionMethod, IsPlaceholder,
                PurchaseDate, PurchasePrice);
            return RedirectToPage();
        }
    }
}
