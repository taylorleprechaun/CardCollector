namespace CardCollector.ViewModels
{
    /// <summary>One row of an analytics roll-up: a label with its event count and match record.</summary>
    public sealed record AnalyticsRecordRow(string Label, int EventCount, WinLossTie Record);
}
