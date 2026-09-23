using CardCollector.Services;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CardCollector.Pages
{
    public abstract class SearchablePageModel : PageModel
    {
        public virtual int ActiveFilterCount =>
            (string.IsNullOrWhiteSpace(CardType) ? 0 : 1)
            + (string.IsNullOrWhiteSpace(RarityName) ? 0 : 1)
            + (string.IsNullOrWhiteSpace(SetName) ? 0 : 1);

        [BindProperty(SupportsGet = true)]
        public string? CardType { get; set; }

        public virtual bool HasActiveFilters =>
            !string.IsNullOrWhiteSpace(CardType)
            || !string.IsNullOrWhiteSpace(RarityName)
            || !string.IsNullOrWhiteSpace(SetName);

        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;

        [BindProperty(SupportsGet = true)]
        public int PageSize { get; set; } = Paging.DEFAULT_PAGE_SIZE;

        [BindProperty(SupportsGet = true)]
        public string? Query { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? RarityName { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SetName { get; set; }

        protected abstract ICardService CardService { get; }
        public virtual IReadOnlyDictionary<string, string?> GetPaginationParams() =>
            new Dictionary<string, string?>
            {
                ["cardType"] = CardType,
                ["rarityName"] = RarityName,
                ["setName"] = SetName
            };
        public IActionResult OnGetAutocomplete(string? q)
        {
            if (string.IsNullOrWhiteSpace(q))
                return new JsonResult(Array.Empty<string>());

            return new JsonResult(CardService.GetCardNameSuggestions(q));
        }

        protected void NormalizeSearchParameters()
        {
            PageNumber = Paging.ClampPage(PageNumber);
            PageSize = Paging.NormalizePageSize(PageSize);

            Query = Query?.Trim();
            CardType = CardType?.Trim();
            RarityName = RarityName?.Trim();
            SetName = SetName?.Trim();
        }
    }
}
