using CardCollector.Services;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;

namespace CardCollector.Pages
{
    public sealed class StatsModel : PageModel
    {
        private readonly ICardService _cardService;

        /// <summary>The cards with price history, as JSON for the price-history picker.</summary>
        public string PriceHistoryCardsJson { get; private set; } = "[]";

        public CollectionStatsViewModel Stats { get; private set; } = new();

        public StatsModel(ICardService cardService)
        {
            _cardService = cardService;
        }

        public async Task OnGetAsync()
        {
            Stats = await _cardService.GetCollectionStatsAsync();
            var cards = await _cardService.GetPriceHistoryCardsAsync();
            PriceHistoryCardsJson = JsonSerializer.Serialize(cards, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
    }
}
