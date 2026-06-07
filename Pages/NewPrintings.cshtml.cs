using CardCollector.Services;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CardCollector.Pages
{
    public sealed class NewPrintingsModel : PageModel
    {
        private readonly ICardService _cardService;

        private static readonly int[] _validPageSizes = [10, 25, 50, 100];

        [BindProperty]
        public int CardID { get; set; }

        [BindProperty]
        public int ImageID { get; set; }

        [BindProperty]
        public string NewRarityName { get; set; } = string.Empty;

        [BindProperty]
        public string NewSetCode { get; set; } = string.Empty;

        public PagedResult<NewPrintingOpportunityViewModel> Opportunities { get; private set; } = new();

        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;

        [BindProperty(SupportsGet = true)]
        public int PageSize { get; set; } = 25;

        [BindProperty]
        public string RarityName { get; set; } = string.Empty;

        [BindProperty]
        public IReadOnlyList<string> RarityNames { get; set; } = [];

        [BindProperty]
        public string SetCode { get; set; } = string.Empty;

        [BindProperty]
        public IReadOnlyList<string> SetCodes { get; set; } = [];

        public NewPrintingsModel(ICardService cardService)
        {
            _cardService = cardService;
        }

        public async Task OnGetAsync()
        {
            if (PageNumber < 1) PageNumber = 1;
            if (!_validPageSizes.Contains(PageSize)) PageSize = 25;

            var all = await _cardService.GetNewPrintingOpportunitiesAsync();
            Opportunities = new PagedResult<NewPrintingOpportunityViewModel>
            {
                Items = all.Skip((PageNumber - 1) * PageSize).Take(PageSize).ToList(),
                Page = PageNumber,
                PageSize = PageSize,
                TotalCount = all.Count
            };
        }

        public async Task<IActionResult> OnPostDismissAsync()
        {
            await _cardService.DismissNewPrintingAsync(CardID, SetCode, RarityName);
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDismissAllAsync()
        {
            for (var i = 0; i < SetCodes.Count && i < RarityNames.Count; i++)
                await _cardService.DismissNewPrintingAsync(CardID, SetCodes[i], RarityNames[i]);
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUpgradeAsync()
        {
            await _cardService.UpgradePreferredVersionAsync(ImageID, CardID, NewSetCode, NewRarityName);
            return RedirectToPage();
        }
    }
}
