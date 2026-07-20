using CardCollector.Data.Models;
using CardCollector.Services;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CardCollector.Extensions
{
    public static class EditionWarningExtensions
    {
        // Non-blocking nudge: the live pricing dataset can legitimately be missing obscure/promo
        // printings that are still real, so this warns instead of rejecting the entry.
        public static async Task WarnIfEditionMismatchAsync(
            this PageModel page, ICardService cardService, int cardID, string setCode, string? rarityName, CardEdition? edition)
        {
            if (edition is null || string.IsNullOrWhiteSpace(rarityName))
                return;

            var category = await cardService.CheckEntryEditionAsync(cardID, setCode, rarityName, edition.Value).ConfigureAwait(false);
            if (category == EditionAuditCategory.EditionMismatch)
                page.TempData["Warning"] = BuildEditionMismatchMessage(edition.Value, setCode, rarityName);
        }

        public static string BuildEditionMismatchMessage(CardEdition edition, string setCode, string rarityName) =>
            $"TCGPlayer doesn't list a {edition.GetDisplayName()} printing of {setCode} {rarityName} for this card — double-check the edition you selected.";
    }
}
