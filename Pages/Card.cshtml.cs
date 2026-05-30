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

        [BindProperty(SupportsGet = true)]
        public int ImageID { get; set; }

        [BindProperty]
        public bool IsPlaceholder { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? ReturnUrl { get; set; }

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

        public async Task<IActionResult> OnPostOrderAsync(
            int quantity = 1, CardCondition? condition = null, CardEdition? edition = null,
            AcquisitionMethod? acquisitionMethod = null,
            DateTime? purchaseDate = null, decimal? purchasePrice = null, decimal? marketPriceAtEntry = null,
            bool setAsPreferred = false, string? rarityName = null)
        {
            await _cardService.AddEntryAsync(
                CardID, ImageID, SetCode, CollectionStatus.Ordered,
                quantity, condition, edition,
                acquisitionMethod, IsPlaceholder,
                purchaseDate, purchasePrice, marketPriceAtEntry, rarityName);

            if (setAsPreferred)
                await _cardService.SavePreferredVersionAsync(CardID, ImageID, SetCode);

            return RedirectToPage(new { ID, ReturnUrl });
        }

        public async Task<IActionResult> OnPostOwnAsync(
            int quantity = 1, CardCondition? condition = null, CardEdition? edition = null,
            AcquisitionMethod? acquisitionMethod = null,
            DateTime? purchaseDate = null, decimal? purchasePrice = null, decimal? marketPriceAtEntry = null,
            bool setAsPreferred = false, string? rarityName = null)
        {
            await _cardService.AddEntryAsync(
                CardID, ImageID, SetCode, CollectionStatus.Owned,
                quantity, condition, edition,
                acquisitionMethod, IsPlaceholder,
                purchaseDate, purchasePrice, marketPriceAtEntry, rarityName);

            if (setAsPreferred)
                await _cardService.SavePreferredVersionAsync(CardID, ImageID, SetCode);

            return RedirectToPage(new { ID, ReturnUrl });
        }
    }
}
