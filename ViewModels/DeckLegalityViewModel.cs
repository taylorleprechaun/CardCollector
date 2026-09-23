using CardCollector.Data.Models;
using CardCollector.Services;

namespace CardCollector.ViewModels
{
    /// <summary>The active tab's legality result plus what the tabs and pickers need to render.</summary>
    public sealed class DeckLegalityViewModel
    {
        public DeckLegality? ActiveLegality { get; init; }

        public required DeckLegalityView ActiveView { get; init; }

        /// <summary>Every known list's effective date, newest first, for the list picker.</summary>
        public required IReadOnlyList<DateOnly> AvailableListDates { get; init; }

        /// <summary>The event "At event" resolves its list from. Null when the deck has no events.</summary>
        public Event? AtEventSource { get; init; }

        public required int DeckID { get; init; }

        /// <summary>The deck's events, newest first, for the event picker.</summary>
        public required IReadOnlyList<Event> Events { get; init; }

        public required bool IsAvailable { get; init; }

        /// <summary>The list date explicitly requested for the active view, if any — distinct from
        /// <see cref="ActiveLegality"/>'s resolved date, so the picker shows "Auto" until the viewer actually picks one.</summary>
        public DateOnly? RequestedListDate { get; init; }
    }
}
