using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CardCollector.Pages
{
    public abstract class SearchablePageModel : PageModel
    {
        protected static readonly int[] ValidPageSizes = [10, 25, 50, 100];

        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;

        [BindProperty(SupportsGet = true)]
        public int PageSize { get; set; } = 25;

        [BindProperty(SupportsGet = true)]
        public string? Query { get; set; }

        protected void NormalizeSearchParameters()
        {
            if (PageNumber < 1) PageNumber = 1;
            if (!ValidPageSizes.Contains(PageSize)) PageSize = 25;
        }
    }
}
