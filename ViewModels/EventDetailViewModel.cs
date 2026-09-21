using CardCollector.Data.Models;

namespace CardCollector.ViewModels
{
    /// <summary>An event with its rounds, the format its date falls in, and the tallies for its rounds.</summary>
    public sealed class EventDetailViewModel
    {
        public required DiceRecord DiceRecord { get; init; }

        public required Event Event { get; init; }

        /// <summary>The format containing the event's date, or null when none does.</summary>
        public Format? Format { get; init; }

        public required WinLossTie GameRecord { get; init; }

        public required WinLossTie MatchRecord { get; init; }

        /// <summary>How many other events have this event's decklist URL and no deck; 0 when it has no URL.</summary>
        public int OtherUnlinkedEventsWithSameURL { get; init; }
    }
}
