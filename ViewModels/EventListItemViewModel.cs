using CardCollector.Data.Models;

namespace CardCollector.ViewModels
{
    /// <summary>One row of the Events list: the event plus what is derived from its date and rounds.</summary>
    public sealed class EventListItemViewModel
    {
        public required Event Event { get; init; }

        /// <summary>Name of the format containing the event's date, or a placeholder when none does.</summary>
        public required string FormatName { get; init; }

        /// <summary>How many other events have this event's decklist URL and no deck; 0 when it has no URL.</summary>
        public int OtherUnlinkedEventsWithSameURL { get; init; }

        public required WinLossTie Record { get; init; }

        public int RoundCount => Event.Matches.Count;
    }
}
