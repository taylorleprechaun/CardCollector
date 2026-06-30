using CardCollector.DTO;
using CardCollector.Repository;
using CardCollector.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CardCollector.Pages
{
    public sealed class DiscoverModel : PageModel
    {
        private readonly ICardService _cardService;
        private readonly ICardSetRepository _cardSetRepository;

        [BindProperty]
        public int CardID { get; set; }

        public Card? CurrentCard { get; private set; }

        public Image? CurrentImage { get; private set; }

        [BindProperty]
        public int ImageID { get; set; }

        public bool IsComplete { get; private set; }

        [BindProperty]
        public string? RarityName { get; set; }

        [BindProperty]
        public string SetCode { get; set; } = string.Empty;

        public DiscoverModel(ICardService cardService, ICardSetRepository cardSetRepository)
        {
            _cardService = cardService;
            _cardSetRepository = cardSetRepository;
        }

        public string GetTCGDate(string setCode) =>
            _cardSetRepository.GetTCGDateBySetCode(setCode) ?? string.Empty;

        public async Task OnGetAsync()
        {
            var card = await _cardService.GetRandomUncollectedAsync();
            if (card is null)
            {
                IsComplete = true;
                return;
            }

            CurrentCard = card;
            CurrentImage = card.CardImages?.FirstOrDefault();
        }

        public async Task<IActionResult> OnPostSetPreferredAsync()
        {
            await _cardService.SavePreferredVersionAsync(CardID, ImageID, SetCode, RarityName);
            return RedirectToPage();
        }
    }
}
