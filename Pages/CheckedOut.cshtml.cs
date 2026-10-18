using CardCollector.Extensions;
using CardCollector.Services;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace CardCollector.Pages
{
    public sealed class CheckedOutModel : SearchablePageModel
    {
        private readonly ICardService _cardService;
        private readonly IRazorPartialRenderer _razorPartialRenderer;

        public CheckedOutModel(ICardService cardService, IRazorPartialRenderer razorPartialRenderer)
        {
            _cardService = cardService;
            _razorPartialRenderer = razorPartialRenderer;
        }

        public PagedResult<CheckedOutCardViewModel> Results { get; private set; } = new();
        protected override ICardService CardService => _cardService;

        public string GetFilterParams()
        {
            return $"cardType={Uri.EscapeDataString(CardType ?? string.Empty)}&setName={Uri.EscapeDataString(SetName ?? string.Empty)}&rarityName={Uri.EscapeDataString(RarityName ?? string.Empty)}&pageNumber={PageNumber}&pageSize={PageSize}&query={Uri.EscapeDataString(Query ?? string.Empty)}";
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

        public async Task<IActionResult> OnPostCheckInAsync(int printingID)
        {
            await _cardService.CheckInCardAsync(printingID).ConfigureAwait(false);
            return await RespondAfterMutationAsync(printingID).ConfigureAwait(false);
        }

        public async Task<IActionResult> OnPostCheckOutAsync(int printingID, int quantity)
        {
            if (quantity >= 1)
                await _cardService.CheckOutCardAsync(printingID, quantity).ConfigureAwait(false);
            return await RespondAfterMutationAsync(printingID).ConfigureAwait(false);
        }

        private object BuildFilterRedirect()
        {
            return new
            {
                cardType = CardType,
                pageNumber = PageNumber,
                pageSize = PageSize,
                query = Query,
                rarityName = RarityName,
                setName = SetName
            };
        }

        private async Task<IActionResult> RespondAfterMutationAsync(int printingID)
        {
            if (!Request.IsAjaxRequest())
                return RedirectToPage(BuildFilterRedirect());

            var criteria = new CheckedOutSearchCriteria
            {
                CardType = CardType,
                Page = 1,
                PageSize = int.MaxValue,
                Query = Query,
                RarityName = RarityName,
                SetName = SetName
            };

            var results = await _cardService.SearchCheckedOutAsync(criteria).ConfigureAwait(false);
            Response.Headers["X-Total-Count"] = results.TotalCount.ToString();

            var match = results.Items.FirstOrDefault(i => i.PrintingID == printingID);
            if (match is null)
                return Content(string.Empty, "text/html");

            var html = await _razorPartialRenderer.RenderPartialAsync(this, "_CheckedOutRow", new CheckedOutRowViewModel
            {
                FilterParams = GetFilterParams(),
                Item = match
            }).ConfigureAwait(false);

            return Content(html, "text/html");
        }
    }
}
