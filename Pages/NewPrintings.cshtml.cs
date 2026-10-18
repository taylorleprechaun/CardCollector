using CardCollector.Services;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CardCollector.Pages
{
    public sealed class NewPrintingsModel : PageModel
    {
        private readonly ICardService _cardService;

        public NewPrintingsModel(ICardService cardService)
        {
            _cardService = cardService;
        }

        /// <summary>The newer catalog printing a tracked printing moves to.</summary>
        [BindProperty]
        public int NewPrintingID { get; set; }

        public PagedResult<NewPrintingOpportunityViewModel> Opportunities { get; private set; } = new();

        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;

        [BindProperty(SupportsGet = true)]
        public int PageSize { get; set; } = Paging.DEFAULT_PAGE_SIZE;

        [BindProperty]
        public int PreferredVersionID { get; set; }

        /// <summary>The catalog printing being dismissed.</summary>
        [BindProperty]
        public int PrintingID { get; set; }

        /// <summary>Every newer catalog printing of a card, dismissed together.</summary>
        [BindProperty]
        public IReadOnlyList<int> PrintingIDs { get; set; } = [];

        public async Task OnGetAsync()
        {
            PageNumber = Paging.ClampPage(PageNumber);
            PageSize = Paging.NormalizePageSize(PageSize);

            var all = await _cardService.GetNewPrintingOpportunitiesAsync();
            Opportunities = new PagedResult<NewPrintingOpportunityViewModel>
            {
                Items = all.Skip((PageNumber - 1) * PageSize).Take(PageSize).ToList(),
                Page = PageNumber,
                PageSize = PageSize,
                TotalCount = all.Count
            };
        }

        public async Task<IActionResult> OnPostDismissAllAsync()
        {
            await _cardService.DismissNewPrintingsAsync(PrintingIDs);
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDismissAsync()
        {
            await _cardService.DismissNewPrintingsAsync([PrintingID]);
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUpgradeAsync()
        {
            await _cardService.UpgradePreferredVersionAsync(PreferredVersionID, NewPrintingID);
            return RedirectToPage();
        }
    }
}
