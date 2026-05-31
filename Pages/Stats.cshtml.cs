using CardCollector.Services;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CardCollector.Pages
{
    public sealed class StatsModel : PageModel
    {
        private readonly ICardService _cardService;

        public CollectionStatsViewModel Stats { get; private set; } = new();

        public StatsModel(ICardService cardService)
        {
            _cardService = cardService;
        }

        public async Task OnGetAsync()
        {
            Stats = await _cardService.GetCollectionStatsAsync();
        }
    }
}
