using CardCollector.Data.Models;
using CardCollector.DTO;
using CardCollector.Services;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CardCollector.Extensions
{
    /// <summary>Warns a page's user when a collection entry records an edition TCGPlayer doesn't list for its printing.</summary>
    public static class EditionWarningExtensions
    {
        /// <summary>The warning shown when TCGPlayer doesn't list the edition for the printing.</summary>
        /// <param name="edition">The edition the user picked.</param>
        /// <param name="setCode">The printing's set code.</param>
        /// <param name="rarityName">The printing's rarity name.</param>
        /// <returns>The message text.</returns>
        public static string BuildEditionMismatchMessage(CardEdition edition, string setCode, string rarityName) =>
                    $"TCGPlayer doesn't list a {edition.GetDisplayName()} printing of {setCode} {rarityName} for this card — double-check the edition you selected.";

        /// <summary>
        /// Sets a warning in the page's <c>TempData</c> when TCGPlayer lists the catalog printing, but not in
        /// <paramref name="edition"/>.
        /// </summary>
        /// <remarks>
        /// A non-blocking nudge: the price data can legitimately miss obscure or promo printings that are still real, so this
        /// warns instead of rejecting the entry. A printing with no TCGPlayer product at all gets no warning.
        /// </remarks>
        /// <param name="page">The page whose next render shows the warning.</param>
        /// <param name="cardService">Checks the edition and supplies the printing's set code and rarity for the message.</param>
        /// <param name="printingID">The catalog printing the entry is for.</param>
        /// <param name="edition">The edition the user picked; null skips the check.</param>
        /// <returns>A task that completes once the check (and any warning) is done.</returns>
        public static async Task WarnIfEditionMismatchAsync(this PageModel page, ICardService cardService, int printingID, CardEdition? edition)
        {
            if (edition is null)
                return;

            var category = await cardService.CheckEntryEditionAsync(printingID, edition.Value).ConfigureAwait(false);
            if (category == EditionAuditCategory.EditionMismatch && cardService.GetPrintingByID(printingID) is { } printing)
                page.TempData["Warning"] = BuildEditionMismatchMessage(
                    edition.Value, printing.Printing.Code ?? string.Empty, RarityExtensions.NormalizeRarityName(printing.Printing.RarityName) ?? string.Empty);
        }

        /// <summary>
        /// Sets a warning in the page's <c>TempData</c> when TCGPlayer lists the printing named by its card, set code, and
        /// rarity, but not in <paramref name="edition"/>. For callers that don't have the catalog printing ID.
        /// </summary>
        /// <remarks>
        /// A non-blocking nudge, for the same reason as the printing-ID overload: missing price data shouldn't stop a real
        /// printing from being recorded.
        /// </remarks>
        /// <param name="page">The page whose next render shows the warning.</param>
        /// <param name="cardService">Checks the edition against TCGPlayer's listings.</param>
        /// <param name="cardID">The catalog card.</param>
        /// <param name="setCode">The printing's set code.</param>
        /// <param name="rarityName">The printing's rarity name; blank skips the check.</param>
        /// <param name="edition">The edition the user picked; null skips the check.</param>
        /// <returns>A task that completes once the check (and any warning) is done.</returns>
        public static async Task WarnIfEditionMismatchAsync(this PageModel page, ICardService cardService, int cardID, string setCode, string? rarityName, CardEdition? edition)
        {
            if (edition is null || string.IsNullOrWhiteSpace(rarityName))
                return;

            var category = await cardService.CheckEntryEditionAsync(cardID, setCode, rarityName, edition.Value).ConfigureAwait(false);
            if (category == EditionAuditCategory.EditionMismatch)
                page.TempData["Warning"] = BuildEditionMismatchMessage(edition.Value, setCode, rarityName);
        }
    }
}
