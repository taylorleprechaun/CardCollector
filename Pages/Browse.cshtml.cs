using CardCollector.Services;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CardCollector.Pages
{
    public class BrowseModel : PageModel
    {
        private readonly ICardService _cardService;

        private static readonly int[] ValidPageSizes = [10, 25, 50, 100];

        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;

        [BindProperty(SupportsGet = true)]
        public int PageSize { get; set; } = 25;

        [BindProperty(SupportsGet = true)]
        public string? Query { get; set; }

        public PagedResult<CardListItemViewModel> Results { get; private set; } = new();

        public BrowseModel(ICardService cardService)
        {
            _cardService = cardService;
        }

        public async Task OnGetAsync()
        {
            if (PageNumber < 1)
                PageNumber = 1;

            if (!ValidPageSizes.Contains(PageSize))
                PageSize = 25;

            Results = await _cardService.SearchCardsAsync(Query, PageNumber, PageSize);
        }

        public IActionResult OnGetAutocomplete(string? q)
        {
            if (string.IsNullOrWhiteSpace(q))
                return new JsonResult(Array.Empty<string>());

            return new JsonResult(_cardService.GetCardNameSuggestions(q));
        }
    }
}
