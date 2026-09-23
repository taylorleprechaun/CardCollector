using CardCollector.Data.Models;
using CardCollector.Extensions;
using CardCollector.Models;
using CardCollector.Rules;
using CardCollector.Services;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CardCollector.Pages.Tournaments.Events
{
    public sealed class DetailsModel : PageModel
    {
        private const string ROUNDS_FRAGMENT = "rounds";

        private readonly IDeckService _deckService;
        private readonly IEventService _eventService;
        private readonly IMatchService _matchService;
        private readonly IRazorPartialRenderer _razorPartialRenderer;

        /// <summary>True when the stored order of the rounds doesn't match their round labels.</summary>
        public bool AreRoundsOutOfOrder { get; private set; }

        /// <summary>The decks an event can be pointed at instead of importing a new one.</summary>
        public IReadOnlyList<DeckOption> DeckOptions { get; private set; } = [];

        public EventDetailViewModel? Detail { get; private set; }

        [BindProperty(SupportsGet = true)]
        public int ID { get; set; }

        [BindProperty]
        public MatchInputModel Input { get; set; } = new();

        public IReadOnlyList<string> OpponentDecks { get; private set; } = [];

        public DetailsModel(IDeckService deckService, IEventService eventService, IMatchService matchService, IRazorPartialRenderer razorPartialRenderer)
        {
            _deckService = deckService;
            _eventService = eventService;
            _matchService = matchService;
            _razorPartialRenderer = razorPartialRenderer;
        }

        public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
        {
            Detail = await _eventService.GetAsync(ID, cancellationToken).ConfigureAwait(false);
            if (Detail is null)
                return NotFound();

            AreRoundsOutOfOrder = !MatchRules.IsInRoundOrder(Detail.Event.Matches.Select(m => m.Round));
            OpponentDecks = await _matchService.GetOpponentDecksAsync(cancellationToken).ConfigureAwait(false);
            DeckOptions = await _deckService.GetOptionsAsync(cancellationToken).ConfigureAwait(false);

            return Page();
        }

        public async Task<IActionResult> OnPostAddMatchAsync(CancellationToken cancellationToken)
        {
            var errors = GetBindingErrors();
            if (errors.Count > 0)
                return RejectSave(errors);

            var result = await _matchService.AddAsync(ID, BuildMatch(), cancellationToken).ConfigureAwait(false);
            return await RespondToSaveAsync(result, "Round added.").ConfigureAwait(false);
        }

        public async Task<IActionResult> OnPostDeleteMatchAsync(int matchID, CancellationToken cancellationToken)
        {
            var summary = await _matchService.DeleteAsync(ID, matchID, cancellationToken).ConfigureAwait(false);
            if (summary is null)
                return RespondNotFound("That round no longer exists.");

            if (!Request.IsAjaxRequest())
            {
                TempData["Success"] = "Round deleted.";
                return RedirectToRounds();
            }

            return new JsonResult(new
            {
                nextRound = summary.NextRound,
                roundCount = summary.RoundCount,
                summaryHtml = await RenderSummaryAsync(summary).ConfigureAwait(false)
            });
        }

        public async Task<IActionResult> OnPostEditMatchAsync(CancellationToken cancellationToken)
        {
            var errors = GetBindingErrors();
            if (errors.Count > 0)
                return RejectSave(errors);

            var result = await _matchService.UpdateAsync(ID, BuildMatch(), cancellationToken).ConfigureAwait(false);
            return await RespondToSaveAsync(result, "Round updated.").ConfigureAwait(false);
        }

        public async Task<IActionResult> OnPostSortMatchesAsync(CancellationToken cancellationToken)
        {
            var sorted = await _matchService.SortAsync(ID, cancellationToken).ConfigureAwait(false);

            TempData["Success"] = sorted ? "Rounds sorted." : "Rounds were already in order.";
            return RedirectToRounds();
        }

        private Match BuildMatch()
        {
            var gamesLost = Input.GamesLost ?? 0;
            var gamesTied = Input.GamesTied ?? 0;
            var gamesWon = Input.GamesWon ?? 0;

            return new Match
            {
                EventID = ID,
                GamesLost = gamesLost,
                GamesTied = gamesTied,
                GamesWon = gamesWon,
                ID = Input.ID,
                IsBye = Input.IsBye,
                Notes = Input.Notes,
                OpponentDeck = Input.OpponentDeck ?? string.Empty,
                Result = Input.Result ?? MatchRules.SuggestResult(gamesWon, gamesLost, gamesTied),
                Round = Input.Round ?? string.Empty,
                WonDiceRoll = Input.WonDiceRoll
            };
        }

        private List<string> GetBindingErrors()
        {
            (string Field, string Message)[] checks =
            [
                (nameof(Input.GamesWon), "Games won must be a whole number."),
                (nameof(Input.GamesLost), "Games lost must be a whole number."),
                (nameof(Input.GamesTied), "Games tied must be a whole number."),
                (nameof(Input.Result), "Result is not valid."),
                (nameof(Input.WonDiceRoll), "Dice roll is not valid.")
            ];

            return checks
                .Where(check => ModelState.IsFieldInvalid(nameof(Input), check.Field))
                .Select(check => check.Message)
                .ToList();
        }

        private IActionResult RedirectToRounds() =>
            RedirectToPage(null, null, new { id = ID }, ROUNDS_FRAGMENT);

        private IActionResult RejectSave(IReadOnlyList<string> errors)
        {
            if (Request.IsAjaxRequest())
                return BadRequest(new { errors });

            TempData["Error"] = string.Join(" ", errors);
            return RedirectToRounds();
        }

        private Task<string> RenderSummaryAsync(MatchSummaryViewModel summary) =>
            _razorPartialRenderer.RenderPartialAsync(this, "_MatchSummary", summary);

        private IActionResult RespondNotFound(string message)
        {
            if (Request.IsAjaxRequest())
                return NotFound();

            TempData["Error"] = message;
            return RedirectToRounds();
        }

        private async Task<IActionResult> RespondToSaveAsync(MatchSaveResult result, string successMessage)
        {
            if (result.NotFound)
                return RespondNotFound(result.Errors[0]);

            if (!result.Succeeded)
                return RejectSave(result.Errors);

            if (!Request.IsAjaxRequest())
            {
                TempData["Success"] = successMessage;
                return RedirectToRounds();
            }

            var summary = result.Summary!;
            return new JsonResult(new
            {
                nextRound = summary.NextRound,
                previousMatchId = result.PreviousMatchID,
                roundCount = summary.RoundCount,
                rowHtml = await _razorPartialRenderer.RenderPartialAsync(this, "_MatchRow", result.Match!).ConfigureAwait(false),
                summaryHtml = await RenderSummaryAsync(summary).ConfigureAwait(false)
            });
        }
    }
}
