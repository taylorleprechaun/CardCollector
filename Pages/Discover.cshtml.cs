using CardCollector.DTO;
using CardCollector.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CardCollector.Pages
{
    public sealed class DiscoverModel : PageModel
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
        public string? RarityName { get; set; }

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

        public async Task<IActionResult> OnPostSetPreferredAsync()
        {
            await _cardService.SavePreferredVersionAsync(CardID, ImageID, SetCode, RarityName);
            return RedirectToPage();
        }
    }
}
