using CardCollector.Data.Models.Catalog;
using CardCollector.Models;
using CardCollector.Services;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CardCollector.Pages.Catalog
{
    /// <remarks>
    /// Every action is a plain form post that redirects back with a message. A failed field edit re-renders the page
    /// instead, so the values typed in aren't lost.
    /// </remarks>
    public sealed class CardDetailsModel : PageModel
    {
        private const string PRINTINGS_FRAGMENT = "printings";

        private readonly ICatalogAdminService _catalogAdminService;

        public CardDetailsModel(ICatalogAdminService catalogAdminService)
        {
            _catalogAdminService = catalogAdminService;
        }

        public CatalogCardDetailsViewModel? Card { get; private set; }

        /// <summary>Why the last field edit was refused; shown above the form.</summary>
        public string? EditError { get; private set; }

        [BindProperty(SupportsGet = true)]
        public int ID { get; set; }

        [BindProperty]
        public CatalogCardEditInput Input { get; set; } = new();

        [BindProperty]
        public CatalogPrintingInput NewPrinting { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            Card = await _catalogAdminService.GetCardAsync(ID).ConfigureAwait(false);
            if (Card is null)
                return NotFound();

            Input = Card.Fields;
            return Page();
        }

        public async Task<IActionResult> OnPostAddPrintingAsync() =>
            Respond(await _catalogAdminService.AddPrintingAsync(ID, NewPrinting).ConfigureAwait(false), "Printing added.", PRINTINGS_FRAGMENT);

        public async Task<IActionResult> OnPostCardStatusAsync(CatalogStatus status) =>
            Respond(
                await _catalogAdminService.SetCardStatusAsync(ID, status).ConfigureAwait(false),
                status == CatalogStatus.Retired ? "Card retired." : "Card restored.");

        public async Task<IActionResult> OnPostMergeAsync(int printingID, int intoPrintingID) =>
            Respond(
                await _catalogAdminService.MergePrintingAsync(printingID, intoPrintingID).ConfigureAwait(false),
                "Printing merged; its rows and links moved over and it is now retired.",
                PRINTINGS_FRAGMENT);

        public async Task<IActionResult> OnPostPrintingStatusAsync(int printingID, CatalogStatus status) =>
            Respond(
                await _catalogAdminService.SetPrintingStatusAsync(printingID, status).ConfigureAwait(false),
                status == CatalogStatus.Retired ? "Printing retired." : "Printing restored.",
                PRINTINGS_FRAGMENT);

        public async Task<IActionResult> OnPostSaveAsync()
        {
            var result = await _catalogAdminService.UpdateCardAsync(ID, Input).ConfigureAwait(false);
            if (result.Succeeded)
                return Respond(result, "Card saved. Edited fields are locked against the sync.");
            if (result.NotFound)
                return NotFound();

            Card = await _catalogAdminService.GetCardAsync(ID).ConfigureAwait(false);
            if (Card is null)
                return NotFound();

            EditError = string.Join(" ", result.Errors);
            return Page();
        }

        public async Task<IActionResult> OnPostUnlinkAsync(int sourceID) =>
            Respond(
                await _catalogAdminService.UnlinkSourceAsync(sourceID).ConfigureAwait(false),
                "Product unlinked. If it came from the review inbox, its item is open there again.",
                PRINTINGS_FRAGMENT);

        public async Task<IActionResult> OnPostUnlockAsync(string field) =>
            Respond(await _catalogAdminService.UnlockFieldAsync(ID, field).ConfigureAwait(false), "Unlocked; the next sync can update it again.");

        private IActionResult Respond(SaveResult result, string successMessage, string? fragment = null)
        {
            if (result.Succeeded)
                TempData["Success"] = successMessage;
            else
                TempData["Warning"] = string.Join(" ", result.Errors);

            return RedirectToPage(null, null, new { id = ID }, fragment);
        }
    }
}
