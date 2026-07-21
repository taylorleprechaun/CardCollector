using System.Text.Json;
using CardCollector.Data.Models;
using CardCollector.Repository;
using CardCollector.Services;
using Microsoft.AspNetCore.Http;

namespace CardCollector
{
    /// <summary>
    /// Handler bodies for the minimal-API endpoints registered in Program.cs, extracted so they're
    /// directly unit-testable without spinning up the ASP.NET pipeline.
    /// </summary>
    public static class APIEndpoints
    {
        public static async Task<IResult> GetCardPriceHistoryAsync(string cardName, ICardService cardService)
        {
            if (string.IsNullOrWhiteSpace(cardName))
                return Results.BadRequest("cardName is required.");

            var history = await cardService.GetCardPriceHistoryAsync(cardName).ConfigureAwait(false);
            return Results.Json(history.Select(s => new { label = s.Label, dates = s.Dates, values = s.Values }));
        }

        public static async Task<IResult> GetPriceAsync(int cardID, string setCode, string rarityName, string? edition, IPricingService pricingService)
        {
            CardEdition? parsedEdition = Enum.TryParse<CardEdition>(edition, out var e) ? e : null;
            var price = await pricingService.GetPrintingPriceAsync(cardID, setCode, rarityName, parsedEdition).ConfigureAwait(false);
            return Results.Json(new { price });
        }
        public static async Task RefreshCardDataStreamAsync(ICardDataRepository cardDataRepository, Func<string, string, Task> send, CancellationToken ct)
        {
            await send("start", "{}").ConfigureAwait(false);
            await cardDataRepository.RefreshAsync().ConfigureAwait(false);
            var count = cardDataRepository.GetBrowseableCards().Count();
            await send("complete", JsonSerializer.Serialize(new { cardCount = count })).ConfigureAwait(false);
        }

        public static async Task<IResult> RefreshPricingDataAsync(IPricingDataCache pricingDataCache)
        {
            await pricingDataCache.RefreshAsync().ConfigureAwait(false);
            return Results.Ok();
        }
    }
}
