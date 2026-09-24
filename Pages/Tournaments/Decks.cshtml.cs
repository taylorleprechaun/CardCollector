using CardCollector.Extensions;
using CardCollector.Models;
using CardCollector.Services;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CardCollector.Pages.Tournaments
{
    public sealed class DecksModel : PageModel
    {
        private readonly IDeckService _deckService;

        public IReadOnlyList<DeckListItemViewModel> Decks { get; private set; } = [];

        public DecksModel(IDeckService deckService)
        {
            _deckService = deckService;
        }

        public async Task OnGetAsync(CancellationToken cancellationToken)
        {
            Decks = await _deckService.GetAllAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id, string? returnURL, CancellationToken cancellationToken)
        {
            var deleted = await _deckService.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
            if (!deleted)
                return RespondFailure(["That deck no longer exists."], returnURL, notFound: true);

            return RespondSuccess("Deck deleted.", returnURL);
        }

        public async Task<IActionResult> OnPostImportAsync(int? eventID, bool linkOthers, string? name, string? returnURL, string? text, CancellationToken cancellationToken)
        {
            var result = await _deckService
                .ImportAsync(new DeckImportRequest { EventID = eventID, LinkOtherEventsWithSameURL = linkOthers, Name = name, Text = text }, cancellationToken)
                .ConfigureAwait(false);

            if (!result.Succeeded)
                return RespondFailure(result.Errors, returnURL);

            return RespondSuccess(DescribeImport(result), returnURL);
        }

        public async Task<IActionResult> OnPostLinkAsync(int deckID, int eventID, bool linkOthers, string? returnURL, CancellationToken cancellationToken)
        {
            var linked = await _deckService.LinkEventAsync(eventID, deckID, linkOthers, cancellationToken).ConfigureAwait(false);
            if (linked == 0)
                return RespondFailure(["That event or deck no longer exists."], returnURL, notFound: true);

            return RespondSuccess(linked == 1 ? "Deck linked." : $"Deck linked to {linked} events.", returnURL);
        }

        public IActionResult OnPostParse(string? text)
        {
            var preview = _deckService.Preview(text);
            if (!preview.Succeeded)
                return BadRequest(new { errors = preview.Errors });

            return new JsonResult(new
            {
                extra = preview.ExtraCount,
                main = preview.MainCount,
                side = preview.SideCount,
                unknown = preview.UnknownPasscodes.Count
            });
        }

        public async Task<IActionResult> OnPostRenameAsync(int id, string? name, string? notes, string? returnURL, CancellationToken cancellationToken)
        {
            var result = await _deckService.UpdateAsync(id, name, notes, cancellationToken).ConfigureAwait(false);
            if (result.NotFound)
                return RespondFailure(result.Errors, returnURL, notFound: true);

            if (!result.Succeeded)
                return RespondFailure(result.Errors, returnURL);

            return RespondSuccess("Deck updated.", returnURL);
        }

        public async Task<IActionResult> OnPostUnlinkAsync(int eventID, string? returnURL, CancellationToken cancellationToken)
        {
            var unlinked = await _deckService.UnlinkEventAsync(eventID, cancellationToken).ConfigureAwait(false);
            if (!unlinked)
                return RespondFailure(["That event no longer exists."], returnURL, notFound: true);

            return RespondSuccess("Deck removed from the event.", returnURL);
        }

        private static string DescribeImport(DeckImportResult result)
        {
            var message = $"Deck added: {result.MainCount} main · {result.ExtraCount} extra · {result.SideCount} side.";
            if (result.UnknownPasscodes.Count > 0)
                message += $" {result.UnknownPasscodes.Count} {(result.UnknownPasscodes.Count == 1 ? "card" : "cards")} couldn't be matched and show as unknown.";

            if (result.LinkedEventCount > 0)
                message += $" Linked to {result.LinkedEventCount} {(result.LinkedEventCount == 1 ? "event" : "events")}.";

            return message;
        }

        private IActionResult RedirectBack(string? returnURL) =>
            !string.IsNullOrEmpty(returnURL) && Url.IsLocalUrl(returnURL) ? LocalRedirect(returnURL) : RedirectToPage();

        private IActionResult RespondFailure(IReadOnlyList<string> errors, string? returnURL, bool notFound = false)
        {
            if (Request.IsAjaxRequest())
                return notFound ? NotFound(new { errors }) : BadRequest(new { errors });

            TempData["Error"] = string.Join(" ", errors);
            return RedirectBack(returnURL);
        }

        // Set even for an AJAX request: the page reloads afterwards and shows the message from TempData.
        private IActionResult RespondSuccess(string message, string? returnURL)
        {
            TempData["Success"] = message;
            return Request.IsAjaxRequest() ? new JsonResult(new { ok = true }) : RedirectBack(returnURL);
        }
    }
}
