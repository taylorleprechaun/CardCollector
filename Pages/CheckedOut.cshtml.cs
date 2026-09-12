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
            var (cardType, rarityName, setName) = GetSafeFilterQueryValues();
            return $"cardType={Uri.EscapeDataString(cardType ?? string.Empty)}&setName={Uri.EscapeDataString(setName ?? string.Empty)}&rarityName={Uri.EscapeDataString(rarityName ?? string.Empty)}&pageNumber={PageNumber}&pageSize={PageSize}&query={Uri.EscapeDataString(Query ?? string.Empty)}";
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

        public async Task<IActionResult> OnPostCheckInAsync(int cardID, string setCode, string rarityName, string? printVariant = null)
        {
            await _cardService.CheckInCardAsync(cardID, setCode, rarityName, printVariant).ConfigureAwait(false);
            return await RespondAfterMutationAsync(cardID, setCode, rarityName, printVariant).ConfigureAwait(false);
        }

        public async Task<IActionResult> OnPostCheckOutAsync(int cardID, string setCode, string rarityName, int quantity, string? printVariant = null)
        {
            if (quantity >= 1)
                await _cardService.CheckOutCardAsync(cardID, setCode, rarityName, quantity, printVariant).ConfigureAwait(false);
            return await RespondAfterMutationAsync(cardID, setCode, rarityName, printVariant).ConfigureAwait(false);
        }

        private object BuildFilterRedirect()
        {
            var (cardType, rarityName, setName) = GetSafeFilterQueryValues();
            return new
            {
                cardType,
                pageNumber = PageNumber,
                pageSize = PageSize,
                query = Query,
                rarityName,
                setName
            };
        }

        private (string? CardType, string? RarityName, string? SetName) GetSafeFilterQueryValues() => (
            Request.Query["cardType"].FirstOrDefault(),
            Request.Query["rarityName"].FirstOrDefault(),
            Request.Query["setName"].FirstOrDefault());

        private bool IsAjaxRequest() =>
            Request.Headers["X-Requested-With"] == "XMLHttpRequest";

        private async Task<IActionResult> RespondAfterMutationAsync(int cardID, string setCode, string? rarityName, string? printVariant)
        {
            if (!IsAjaxRequest())
                return RedirectToPage(BuildFilterRedirect());

            var (cardType, filterRarityName, setName) = GetSafeFilterQueryValues();
            var criteria = new CheckedOutSearchCriteria
            {
                CardType = cardType,
                Page = 1,
                PageSize = int.MaxValue,
                Query = Query,
                RarityName = filterRarityName,
                SetName = setName
            };

            var results = await _cardService.SearchCheckedOutAsync(criteria).ConfigureAwait(false);
            Response.Headers["X-Total-Count"] = results.TotalCount.ToString();

            var match = results.Items.FirstOrDefault(i =>
                i.CardID == cardID && i.SetCode.Equals(setCode, StringComparison.OrdinalIgnoreCase)
                && (rarityName is null || i.RarityName.Equals(rarityName, StringComparison.OrdinalIgnoreCase))
                && string.Equals(i.PrintVariant ?? string.Empty, printVariant ?? string.Empty, StringComparison.OrdinalIgnoreCase));
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
