using CardCollector.Data.Models;

namespace CardCollector.ViewModels
{
    /// <summary>Filters for the tournament analytics. Text filters are case-insensitive "contains" matches.</summary>
    public sealed class AnalyticsCriteria
    {
        public const int DEFAULT_MIN_MATCHES = 2;

        public DateOnly? DateFrom { get; init; }

        public DateOnly? DateTo { get; init; }

        public string? DeckName { get; init; }

        public EventType? EventType { get; init; }

        /// <summary>Leaves byes out of every record. Matchups and dice rolls never include byes.</summary>
        public bool ExcludeByes { get; init; }

        /// <summary>Matches the format derived from each event's date.</summary>
        public int? FormatID { get; init; }

        public string? Location { get; init; }

        /// <summary>The fewest rounds against an opponent for it to be listed in the matchups.</summary>
        public int MinMatches { get; init; } = DEFAULT_MIN_MATCHES;

        /// <summary>Keeps only the rounds against a matching opponent, and only the events that have one.</summary>
        public string? Opponent { get; init; }
    }
}
