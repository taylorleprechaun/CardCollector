namespace CardCollector.ViewModels
{
    /// <summary>The headline numbers for a set of events.</summary>
    public sealed class AnalyticsSummary
    {
        public int EventCount { get; init; }

        public int FirstPlaceCount { get; init; }

        public WinLossTie GameRecord { get; init; } = new(0, 0, 0);

        public WinLossTie MatchRecord { get; init; } = new(0, 0, 0);
    }
}
