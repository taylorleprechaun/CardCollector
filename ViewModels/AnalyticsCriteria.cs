namespace CardCollector.ViewModels
{
    /// <summary>Filters for the tournament analytics.</summary>
    public sealed class AnalyticsCriteria : EventFilterCriteria
    {
        public const int DEFAULT_MIN_MATCHES = 2;

        /// <summary>Leaves byes out of every record. Matchups and dice rolls never include byes.</summary>
        public bool ExcludeByes { get; init; }

        /// <summary>The fewest rounds against an opponent for it to be listed in the matchups.</summary>
        public int MinMatches { get; init; } = DEFAULT_MIN_MATCHES;

        /// <summary>Keeps only the rounds against a matching opponent, and only the events that have one.</summary>
        public string? Opponent { get; init; }
    }
}
