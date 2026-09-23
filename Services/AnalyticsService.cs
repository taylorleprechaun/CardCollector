using CardCollector.Repository;
using CardCollector.ViewModels;

namespace CardCollector.Services
{
    /// <summary>
    /// There are only a few hundred events, so every event is loaded and filtered in memory rather than aggregated in SQL.
    /// </summary>
    public sealed class AnalyticsService : IAnalyticsService
    {
        private readonly IEventRepository _eventRepository;
        private readonly IFormatService _formatService;

        public AnalyticsService(IEventRepository eventRepository, IFormatService formatService)
        {
            _eventRepository = eventRepository;
            _formatService = formatService;
        }

        public async Task<AnalyticsReport> GetReportAsync(AnalyticsCriteria criteria, CancellationToken cancellationToken = default)
        {
            if (criteria is null) throw new ArgumentNullException(nameof(criteria));

            var events = await _eventRepository.GetAllWithMatchesAsync(cancellationToken).ConfigureAwait(false);
            var formats = await _formatService.GetAllAsync(cancellationToken).ConfigureAwait(false);

            var filtered = AnalyticsCalculator.Filter(events, formats, criteria);
            return AnalyticsCalculator.Calculate(filtered, criteria.MinMatches);
        }
    }
}
