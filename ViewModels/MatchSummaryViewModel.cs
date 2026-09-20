namespace CardCollector.ViewModels
{
    /// <summary>The tallies for an event's rounds, plus the label suggested for the next round.</summary>
    public sealed class MatchSummaryViewModel
    {
        public required DiceRecord DiceRecord { get; init; }

        public required WinLossTie GameRecord { get; init; }

        public required WinLossTie MatchRecord { get; init; }

        /// <summary>Empty when there is nothing sensible to suggest.</summary>
        public string NextRound { get; init; } = string.Empty;

        public int RoundCount { get; init; }
    }
}
