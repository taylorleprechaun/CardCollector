using CardCollector.Data.Models;
using CardCollector.Models;

namespace CardCollector.ViewModels
{
    /// <summary>The active tab's per-card legality plus what the tabs and pickers need to render.</summary>
    public sealed class DeckLegalityViewModel
    {
        public required DeckLegalityView ActiveView { get; init; }

        /// <summary>The event "At event" resolves its list from. Null when the deck has no events.</summary>
        public Event? AtEventSource { get; init; }

        /// <summary>Every known list's effective date, newest first, for the list picker.</summary>
        public required IReadOnlyList<DateOnly> AvailableListDates { get; init; }

        /// <summary>Each restricted card's status against the active tab's list, keyed by card ID; null when no list applies.</summary>
        public IReadOnlyDictionary<int, DeckLegalityCardStatus>? CardStatuses { get; init; }

        public required int DeckID { get; init; }

        /// <summary>The deck's events, newest first, for the event picker.</summary>
        public required IReadOnlyList<Event> Events { get; init; }

        public required bool IsAvailable { get; init; }

        /// <summary>The list date the viewer picked for the active view, if any; null shows "Auto" in the picker.</summary>
        public DateOnly? RequestedListDate { get; init; }
    }
}
