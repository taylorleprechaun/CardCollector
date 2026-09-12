using CardCollector.Data.Models;
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

        public static async Task<IResult> GetPriceAsync(int cardID, string setCode, string rarityName, string? edition, string? printVariant, IPricingService pricingService)
        {
            CardEdition? parsedEdition = Enum.TryParse<CardEdition>(edition, out var e) ? e : null;
            var price = await pricingService.GetPrintingPriceAsync(cardID, setCode, rarityName, parsedEdition, printVariant).ConfigureAwait(false);
            return Results.Json(new { price });
        }
    }
}
