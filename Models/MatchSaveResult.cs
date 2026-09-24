using CardCollector.Data.Models;
using CardCollector.ViewModels;

namespace CardCollector.Models
{
    /// <summary>Outcome of saving a round; validation failures are reported here instead of thrown.</summary>
    public sealed class MatchSaveResult
    {
        public IReadOnlyList<string> Errors { get; }

        /// <summary>The saved round; null unless the save succeeded.</summary>
        public Match? Match { get; }

        /// <summary>True when the event, or the round being edited, does not exist.</summary>
        public bool NotFound { get; }

        /// <summary>For an added round, the round it now follows; null when it came first or the round was not added.</summary>
        public int? PreviousMatchID { get; }

        public bool Succeeded { get; }

        /// <summary>The event's tallies after the save; null unless the save succeeded.</summary>
        public MatchSummaryViewModel? Summary { get; }

        private MatchSaveResult(IReadOnlyList<string> errors, Match? match, bool notFound, int? previousMatchID, bool succeeded, MatchSummaryViewModel? summary)
        {
            Errors = errors;
            Match = match;
            NotFound = notFound;
            PreviousMatchID = previousMatchID;
            Succeeded = succeeded;
            Summary = summary;
        }

        public static MatchSaveResult Failure(IReadOnlyList<string> errors) => new(errors, null, false, null, false, null);

        public static MatchSaveResult Missing(string message) => new([message], null, true, null, false, null);

        public static MatchSaveResult Success(Match match, MatchSummaryViewModel summary, int? previousMatchID = null) =>
            new([], match, false, previousMatchID, true, summary);
    }
}
