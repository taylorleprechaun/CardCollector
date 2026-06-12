using CardCollector.Services;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace CardCollector.Pages
{
    public sealed class CheckedOutModel : SearchablePageModel
    {
        private readonly ICardService _cardService;

        protected override ICardService CardService => _cardService;

        public PagedResult<CheckedOutCardViewModel> Results { get; private set; } = new();

        public CheckedOutModel(ICardService cardService)
        {
            _cardService = cardService;
        }

        public async Task OnGetAsync()
        {
            NormalizeSearchParameters();

            var criteria = new CheckedOutSearchCriteria
            {
                CardType = CardType,
                Page = PageNumber,
                PageSize = PageSize,
                Query = Query,
                RarityName = RarityName,
                SetName = SetName
            };

            Results = await _cardService.SearchCheckedOutAsync(criteria).ConfigureAwait(false);
        }

        public async Task<IActionResult> OnPostCheckInAsync(int imageID, string setCode)
        {
            await _cardService.CheckInCardAsync(imageID, setCode).ConfigureAwait(false);
            return RedirectToPage(BuildFilterRedirect());
        }

        public async Task<IActionResult> OnPostCheckOutAsync(int cardID, int imageID, string setCode, int quantity)
        {
            if (quantity >= 1)
                await _cardService.CheckOutCardAsync(cardID, imageID, setCode, quantity).ConfigureAwait(false);
            return RedirectToPage(BuildFilterRedirect());
        }

        private object BuildFilterRedirect() => new
        {
            cardType = CardType,
            pageNumber = PageNumber,
            pageSize = PageSize,
            query = Query,
            rarityName = RarityName,
            setName = SetName
        };
    }
}
