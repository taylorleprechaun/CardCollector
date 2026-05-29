using CardCollector.Data.Models;
using CardCollector.DTO;
using CardCollector.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CardCollector.Pages
{
    public class CardModel : PageModel
    {
        private readonly ICardService _cardService;

        [BindProperty]
        public int CardID { get; set; }

        public bool CardNotFound { get; private set; }

        public Card? CurrentCard { get; private set; }

        [BindProperty(SupportsGet = true)]
        public int ID { get; set; }

        [BindProperty]
        public int ImageID { get; set; }

        [BindProperty]
        public bool IsPlaceholder { get; set; }

        [BindProperty]
        public decimal? MarketPriceAtEntry { get; set; }

        [BindProperty]
        public DateTime? PurchaseDate { get; set; }

        [BindProperty]
        public decimal? PurchasePrice { get; set; }

        [BindProperty]
        public int Quantity { get; set; } = 1;

        [BindProperty(SupportsGet = true)]
        public string? ReturnUrl { get; set; }

        [BindProperty]
        public AcquisitionMethod? SelectedAcquisitionMethod { get; set; }

        [BindProperty]
        public CardCondition? SelectedCondition { get; set; }

        [BindProperty]
        public CardEdition? SelectedEdition { get; set; }

        [BindProperty]
        public string SetCode { get; set; } = string.Empty;

        public CardModel(ICardService cardService)
        {
            _cardService = cardService;
        }

        public Task OnGetAsync()
        {
            if (ID == 0)
            {
                CardNotFound = true;
                return Task.CompletedTask;
            }

            CurrentCard = _cardService.GetCardByID(ID);
            if (CurrentCard is null)
                CardNotFound = true;

            return Task.CompletedTask;
        }

        public async Task<IActionResult> OnPostOrderAsync()
        {
            await _cardService.AddEntryAsync(
                CardID, ImageID, SetCode, CollectionStatus.Ordered,
                Quantity, SelectedCondition, SelectedEdition,
                SelectedAcquisitionMethod, IsPlaceholder,
                PurchaseDate, PurchasePrice, MarketPriceAtEntry);
            return RedirectToPage(new { ID, ReturnUrl });
        }

        public async Task<IActionResult> OnPostOwnAsync()
        {
            await _cardService.AddEntryAsync(
                CardID, ImageID, SetCode, CollectionStatus.Owned,
                Quantity, SelectedCondition, SelectedEdition,
                SelectedAcquisitionMethod, IsPlaceholder,
                PurchaseDate, PurchasePrice, MarketPriceAtEntry);
            return RedirectToPage(new { ID, ReturnUrl });
        }
    }
}
