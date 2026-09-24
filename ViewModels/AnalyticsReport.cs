namespace CardCollector.ViewModels
{
    /// <summary>Records and roll-ups for a filtered set of events.</summary>
    public sealed class AnalyticsReport
    {
        /// <summary>Most events first.</summary>
        public IReadOnlyList<AnalyticsRecordRow> ByDeck { get; init; } = [];

        /// <summary>Most events first.</summary>
        public IReadOnlyList<AnalyticsRecordRow> ByEventType { get; init; } = [];

        /// <summary>Oldest format first; events outside every format come last.</summary>
        public IReadOnlyList<AnalyticsRecordRow> ByFormat { get; init; } = [];

        /// <summary>Newest format first, then deck name.</summary>
        public IReadOnlyList<DeckFormatRow> DeckFormats { get; init; } = [];

        /// <summary>Match records when the dice roll was won, lost, or not recorded. Byes are left out.</summary>
        public IReadOnlyList<AnalyticsRecordRow> Dice { get; init; } = [];

        public bool IsEmpty => Summary.EventCount == 0;

        /// <summary>Most rounds first; byes are left out.</summary>
        public IReadOnlyList<AnalyticsRecordRow> Matchups { get; init; } = [];

        public AnalyticsSummary Summary { get; init; } = new();
    }
}
