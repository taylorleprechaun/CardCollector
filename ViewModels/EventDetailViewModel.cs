using CardCollector.Data.Models;

namespace CardCollector.ViewModels
{
    /// <summary>An event with its rounds, the format its date falls in, and the tallies for its rounds.</summary>
    public sealed class EventDetailViewModel
    {
        /// <summary>True when the stored order of the rounds doesn't match their round labels.</summary>
        public bool AreRoundsOutOfOrder { get; init; }

        public required Event Event { get; init; }

        /// <summary>The format containing the event's date, or null when none does.</summary>
        public Format? Format { get; init; }

        /// <summary>How many other events have this event's decklist URL and no deck; 0 when it has no URL.</summary>
        public int OtherUnlinkedEventsWithSameURL { get; init; }

        public required MatchSummaryViewModel Summary { get; init; }
    }
}
