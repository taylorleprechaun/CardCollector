using CardCollector.Data.Models;
using CardCollector.DTO;
using CardCollector.Repository;
using CardCollector.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CardCollector.Pages
{
    public sealed class CardModel : PageModel
    {
        private readonly ICardService _cardService;
        private readonly ICardSetRepository _cardSetRepository;

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

        public PreferredVersion? PreferredVersion { get; private set; }

        [BindProperty]
        public string? RarityName { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? ReturnURL { get; set; }

        [BindProperty]
        public string SetCode { get; set; } = string.Empty;

        public CardModel(ICardService cardService, ICardSetRepository cardSetRepository)
        {
            _cardService = cardService;
            _cardSetRepository = cardSetRepository;
        }

        public string GetTCGDate(string setCode) =>
            _cardSetRepository.GetTCGDateBySetCode(setCode) ?? string.Empty;

        public async Task OnGetAsync()
        {
            if (ID == 0)
            {
                CardNotFound = true;
                return;
            }

            CurrentCard = _cardService.GetCardByID(ID);
            if (CurrentCard is null)
            {
                CardNotFound = true;
                return;
            }

            if (ImageID != 0)
                PreferredVersion = await _cardService.GetPreferredVersionByImageIDAsync(ImageID);
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

            return RedirectToPage(new { ID, ReturnURL });
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

            return RedirectToPage(new { ID, ReturnURL });
        }

        public async Task<IActionResult> OnPostRemovePreferredAsync()
        {
            await _cardService.RemoveFromWishlistAsync(ImageID);
            return RedirectToPage(new { ID, imageID = ImageID, ReturnURL });
        }

        public async Task<IActionResult> OnPostSetPreferredAsync()
        {
            await _cardService.SavePreferredVersionAsync(CardID, ImageID, SetCode, RarityName);
            return RedirectToPage(new { ID, imageID = ImageID, ReturnURL });
        }
    }
}
