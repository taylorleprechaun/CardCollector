using CardCollector.Services;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CardCollector.Pages.Tournaments
{
    public sealed class DecksModel : PageModel
    {
        private readonly IDeckService _deckService;

        public DecksModel(IDeckService deckService)
        {
            _deckService = deckService;
        }

        public IReadOnlyList<DeckListItemViewModel> Decks { get; private set; } = [];

        public async Task OnGetAsync(CancellationToken cancellationToken)
        {
            Decks = await _deckService.GetAllAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id, string? returnUrl, CancellationToken cancellationToken)
        {
            var deleted = await _deckService.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
            if (!deleted)
                return RespondFailure(["That deck no longer exists."], returnUrl, notFound: true);

            return RespondSuccess("Deck deleted.", returnUrl);
        }

        public async Task<IActionResult> OnPostImportAsync(int? eventID, bool linkOthers, string? name, string? returnUrl, string? text, CancellationToken cancellationToken)
        {
            var result = await _deckService
                .ImportAsync(new DeckImportRequest { EventID = eventID, LinkOtherEventsWithSameUrl = linkOthers, Name = name, Text = text }, cancellationToken)
                .ConfigureAwait(false);

            if (!result.Succeeded)
                return RespondFailure(result.Errors, returnUrl);

            return RespondSuccess(DescribeImport(result), returnUrl);
        }

        public async Task<IActionResult> OnPostLinkAsync(int deckID, int eventID, bool linkOthers, string? returnUrl, CancellationToken cancellationToken)
        {
            var linked = await _deckService.LinkEventAsync(eventID, deckID, linkOthers, cancellationToken).ConfigureAwait(false);
            if (linked == 0)
                return RespondFailure(["That event or deck no longer exists."], returnUrl, notFound: true);

            return RespondSuccess(linked == 1 ? "Deck linked." : $"Deck linked to {linked} events.", returnUrl);
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

        public async Task<IActionResult> OnPostRenameAsync(int id, string? name, string? notes, string? returnUrl, CancellationToken cancellationToken)
        {
            var result = await _deckService.UpdateAsync(id, name, notes, cancellationToken).ConfigureAwait(false);
            if (result.NotFound)
                return RespondFailure(result.Errors, returnUrl, notFound: true);

            if (!result.Succeeded)
                return RespondFailure(result.Errors, returnUrl);

            return RespondSuccess("Deck updated.", returnUrl);
        }

        public async Task<IActionResult> OnPostUnlinkAsync(int eventID, string? returnUrl, CancellationToken cancellationToken)
        {
            var unlinked = await _deckService.UnlinkEventAsync(eventID, cancellationToken).ConfigureAwait(false);
            if (!unlinked)
                return RespondFailure(["That event no longer exists."], returnUrl, notFound: true);

            return RespondSuccess("Deck removed from the event.", returnUrl);
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

        private bool IsAjaxRequest() =>
            Request.Headers["X-Requested-With"] == "XMLHttpRequest";

        private IActionResult RedirectBack(string? returnUrl) =>
            !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToPage();

        private IActionResult RespondFailure(IReadOnlyList<string> errors, string? returnUrl, bool notFound = false)
        {
            if (IsAjaxRequest())
                return notFound ? NotFound(new { errors }) : BadRequest(new { errors });

            TempData["Error"] = string.Join(" ", errors);
            return RedirectBack(returnUrl);
        }

        // Set even for an AJAX request: the page reloads afterwards and shows the message from TempData.
        private IActionResult RespondSuccess(string message, string? returnUrl)
        {
            TempData["Success"] = message;
            return IsAjaxRequest() ? new JsonResult(new { ok = true }) : RedirectBack(returnUrl);
        }
    }
}
