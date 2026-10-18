using CardCollector.Data.Models;
using CardCollector.Services;

namespace CardCollector
{
    /// <summary>
    /// Handler bodies for the minimal-API endpoints registered in Program.cs, extracted so they're
    /// directly unit-testable without spinning up the ASP.NET pipeline.
    /// </summary>
    public static class APIEndpoints
    {
        /// <summary>Returns a catalog card's value history, one series per collection entry.</summary>
        public static async Task<IResult> GetCardPriceHistoryAsync(int cardID, ICardService cardService)
        {
            if (cardID <= 0)
                return Results.BadRequest("cardID is required.");

            var history = await cardService.GetCardPriceHistoryAsync(cardID).ConfigureAwait(false);
            return Results.Json(history.Select(s => new { label = s.Label, dates = s.Dates, values = s.Values }));
        }

        /// <summary>Returns a catalog printing's market price for an edition (any edition when it's missing or unknown).</summary>
        public static async Task<IResult> GetPriceAsync(int printingID, string? edition, IPricingService pricingService)
        {
            CardEdition? parsedEdition = Enum.TryParse<CardEdition>(edition, out var e) ? e : null;
            var price = await pricingService.GetPrintingPriceAsync(printingID, parsedEdition).ConfigureAwait(false);
            return Results.Json(new { price });
        }
    }
}
