using CardCollector.Repository;
using CardCollector.Services;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CardCollector.Pages
{
    public class BrowseModel : PageModel
    {
        private static readonly int[] ValidPageSizes = [10, 25, 50, 100];

        private readonly ICardDataRepository _cardDataRepository;
        private readonly ICardService _cardService;

        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;

        [BindProperty(SupportsGet = true)]
        public int PageSize { get; set; } = 25;

        [BindProperty(SupportsGet = true)]
        public string? Query { get; set; }

        public PagedResult<CardListItemViewModel> Results { get; private set; } = new();

        public BrowseModel(ICardDataRepository cardDataRepository, ICardService cardService)
        {
            _cardDataRepository = cardDataRepository;
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

            var matches = _cardDataRepository.GetAllCards()
                .Where(c => c.Name.Contains(q, StringComparison.OrdinalIgnoreCase))
                .OrderBy(c => c.Name)
                .Take(10)
                .Select(c => c.Name)
                .ToArray();

            return new JsonResult(matches);
        }
    }
}
