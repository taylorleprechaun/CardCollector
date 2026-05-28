using CardCollector.Services;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CardCollector.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ICardService _cardService;

        public DashboardStats Stats { get; private set; } = new();

        public IndexModel(ICardService cardService)
        {
            _cardService = cardService;
        }

        public async Task OnGetAsync()
        {
            Stats = await _cardService.GetDashboardStatsAsync();
        }
    }
}
