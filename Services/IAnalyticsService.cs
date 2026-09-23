using CardCollector.ViewModels;

namespace CardCollector.Services
{
    /// <summary>
    /// Summarizes tournament results: records, roll-ups by deck, format and event type, matchups and dice rolls.
    /// </summary>
    public interface IAnalyticsService
    {
        /// <summary>
        /// Returns the report for the events matching the criteria. Each event's format is derived from its date.
        /// </summary>
        Task<AnalyticsReport> GetReportAsync(AnalyticsCriteria criteria, CancellationToken cancellationToken = default);
    }
}
