using CardCollector.Data.Models;

namespace CardCollector.ViewModels
{
    /// <summary>
    /// Filters for the Events list. <see cref="FormatID"/> is resolved to a date range by the service
    /// before the repository is queried; the repository only looks at the date bounds.
    /// </summary>
    public sealed class EventSearchCriteria
    {
        public DateOnly? DateFrom { get; init; }

        public DateOnly? DateTo { get; init; }

        /// <summary>Case-insensitive "contains" match on the deck name.</summary>
        public string? DeckName { get; init; }

        public EventType? EventType { get; init; }

        public int? FormatID { get; init; }

        /// <summary>Case-insensitive "contains" match on the location.</summary>
        public string? Location { get; init; }

        public int Page { get; init; } = 1;

        public int PageSize { get; init; } = 25;
    }
}
