using CardCollector.Services;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CardCollector.Pages
{
    public class BrowseModel : SearchablePageModel
    {
        private readonly ICardService _cardService;

        public PagedResult<CardListItemViewModel> Results { get; private set; } = new();

        public BrowseModel(ICardService cardService)
        {
            _cardService = cardService;
        }

        public async Task OnGetAsync()
        {
            NormalizeSearchParameters();
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
