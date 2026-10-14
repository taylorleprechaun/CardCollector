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

        /// <summary>
        /// Returns a printing's market price for an edition. Takes a catalog <paramref name="printingID"/>, or (for pages not
        /// yet moved to printing IDs) the card, set code, rarity, and variant.
        /// </summary>
        public static async Task<IResult> GetPriceAsync(int? printingID, int? cardID, string? setCode, string? rarityName, string? edition, string? printVariant, IPricingService pricingService)
        {
            CardEdition? parsedEdition = Enum.TryParse<CardEdition>(edition, out var e) ? e : null;
            if (printingID is int id)
                return Results.Json(new { price = await pricingService.GetPrintingPriceAsync(id, parsedEdition).ConfigureAwait(false) });

            if (cardID is not int card || string.IsNullOrWhiteSpace(setCode) || string.IsNullOrWhiteSpace(rarityName))
                return Results.BadRequest("printingID, or cardID with setCode and rarityName, is required.");

            var price = await pricingService.GetPrintingPriceAsync(card, setCode, rarityName, parsedEdition, printVariant).ConfigureAwait(false);
            return Results.Json(new { price });
        }
    }
}
