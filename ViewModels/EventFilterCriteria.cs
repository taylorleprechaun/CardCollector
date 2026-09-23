using CardCollector.Data.Models;

namespace CardCollector.ViewModels
{
    /// <summary>The event filters shared by the Events list and Analytics. Text filters are case-insensitive "contains" matches.</summary>
    public abstract class EventFilterCriteria
    {
        public DateOnly? DateFrom { get; init; }

        public DateOnly? DateTo { get; init; }

        public string? DeckName { get; init; }

        public EventType? EventType { get; init; }

        /// <summary>Matches the format derived from each event's date.</summary>
        public int? FormatID { get; init; }

        public string? Location { get; init; }
    }
}
