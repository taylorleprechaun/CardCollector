using CardCollector.Services;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;

namespace CardCollector.Pages
{
    public sealed class StatsModel : PageModel
    {
        private readonly ICardService _cardService;

        public CollectionStatsViewModel Stats { get; private set; } = new();
        public string TrackedCardImageMapJson { get; private set; } = "{}";

        public StatsModel(ICardService cardService)
        {
            _cardService = cardService;
        }

        public async Task OnGetAsync()
        {
            Stats = await _cardService.GetCollectionStatsAsync();
            var imageMap = await _cardService.GetTrackedCardImageMapAsync();
            TrackedCardImageMapJson = JsonSerializer.Serialize(imageMap);
        }
    }
}
