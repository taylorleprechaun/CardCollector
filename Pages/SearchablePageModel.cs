using CardCollector.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CardCollector.Pages
{
    public abstract class SearchablePageModel : PageModel
    {
        protected static readonly int[] ValidPageSizes = [10, 25, 50, 100];

        [BindProperty(SupportsGet = true)]
        public string? CardType { get; set; }

        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;

        [BindProperty(SupportsGet = true)]
        public int PageSize { get; set; } = 25;

        [BindProperty(SupportsGet = true)]
        public string? Query { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? RarityName { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SetName { get; set; }

        protected abstract ICardService CardService { get; }

        public virtual int ActiveFilterCount =>
            (string.IsNullOrWhiteSpace(CardType) ? 0 : 1)
            + (string.IsNullOrWhiteSpace(RarityName) ? 0 : 1)
            + (string.IsNullOrWhiteSpace(SetName) ? 0 : 1);

        public virtual IReadOnlyDictionary<string, string?> GetPaginationParams() =>
            new Dictionary<string, string?>
            {
                ["cardType"] = CardType,
                ["rarityName"] = RarityName,
                ["setName"] = SetName
            };
        
        public virtual bool HasActiveFilters =>
            !string.IsNullOrWhiteSpace(CardType)
            || !string.IsNullOrWhiteSpace(RarityName)
            || !string.IsNullOrWhiteSpace(SetName);

        public IActionResult OnGetAutocomplete(string? q)
        {
            if (string.IsNullOrWhiteSpace(q))
                return new JsonResult(Array.Empty<string>());

            return new JsonResult(CardService.GetCardNameSuggestions(q));
        }

        protected void NormalizeSearchParameters()
        {
            if (PageNumber < 1) PageNumber = 1;
            if (!ValidPageSizes.Contains(PageSize)) PageSize = 25;
        }
    }
}
