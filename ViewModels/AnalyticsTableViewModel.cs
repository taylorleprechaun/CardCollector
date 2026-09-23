namespace CardCollector.ViewModels
{
    /// <summary>A sortable roll-up table on the Analytics page.</summary>
    public sealed class AnalyticsTableViewModel
    {
        public required string Caption { get; init; }

        /// <summary>True to show the number of rounds instead of events (matchups and dice rolls).</summary>
        public bool CountsRounds { get; init; }

        public required string LabelHeader { get; init; }

        public required IReadOnlyList<AnalyticsRecordRow> Rows { get; init; }

        /// <summary>Shown as a footer row that stays last when the table is sorted.</summary>
        public AnalyticsRecordRow? Total { get; init; }
    }
}
