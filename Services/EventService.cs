using CardCollector.Data.Models;
using CardCollector.Models;
using CardCollector.Repository;
using CardCollector.Rules;
using CardCollector.ViewModels;

namespace CardCollector.Services
{
    public sealed class EventService : IEventService
    {
        private readonly IEventRepository _eventRepository;
        private readonly IFormatService _formatService;

        public EventService(IEventRepository eventRepository, IFormatService formatService)
        {
            _eventRepository = eventRepository;
            _formatService = formatService;
        }

        public async Task<SaveResult> AddAsync(Event tournamentEvent, CancellationToken cancellationToken = default)
        {
            if (tournamentEvent is null) throw new ArgumentNullException(nameof(tournamentEvent));

            var normalized = EventRules.Normalize(tournamentEvent);
            normalized.ID = 0;

            var errors = EventRules.Validate(normalized);
            if (errors.Count > 0)
                return SaveResult.Failure(errors);

            await _eventRepository.AddAsync(normalized, cancellationToken).ConfigureAwait(false);
            return SaveResult.Success();
        }

        public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default) =>
            _eventRepository.DeleteAsync(id, cancellationToken);

        public async Task<EventDetailViewModel?> GetAsync(int id, CancellationToken cancellationToken = default)
        {
            var tournamentEvent = await _eventRepository.GetAsync(id, includeMatches: true, cancellationToken).ConfigureAwait(false);
            if (tournamentEvent is null)
                return null;

            var formats = await _formatService.GetAllAsync(cancellationToken).ConfigureAwait(false);
            var unlinkedURLCounts = await GetUnlinkedURLCountsAsync([tournamentEvent], cancellationToken).ConfigureAwait(false);

            return new EventDetailViewModel
            {
                Event = tournamentEvent,
                Format = FormatRules.FindForDate(formats, tournamentEvent.Date),
                OtherUnlinkedEventsWithSameURL = CountOtherUnlinkedEvents(tournamentEvent, unlinkedURLCounts),
                Summary = MatchRules.Summarize(tournamentEvent.Matches)
            };
        }

        public Task<IReadOnlyList<string>> GetDeckNamesAsync(CancellationToken cancellationToken = default) =>
            _eventRepository.GetDeckNamesAsync(cancellationToken);

        public Task<IReadOnlyList<string>> GetLocationsAsync(CancellationToken cancellationToken = default) =>
            _eventRepository.GetLocationsAsync(cancellationToken);

        public async Task<PagedResult<EventListItemViewModel>> SearchAsync(EventSearchCriteria criteria, CancellationToken cancellationToken = default)
        {
            if (criteria is null) throw new ArgumentNullException(nameof(criteria));

            var formats = await _formatService.GetAllAsync(cancellationToken).ConfigureAwait(false);

            var resolved = ResolveFormatFilter(criteria, formats);
            if (resolved is null)
            {
                return new PagedResult<EventListItemViewModel>
                {
                    Page = Math.Max(1, criteria.Page),
                    PageSize = criteria.PageSize
                };
            }

            var result = await _eventRepository.SearchAsync(resolved, cancellationToken).ConfigureAwait(false);
            var unlinkedURLCounts = await GetUnlinkedURLCountsAsync(result.Items, cancellationToken).ConfigureAwait(false);

            return new PagedResult<EventListItemViewModel>
            {
                Items = result.Items.Select(e => ToListItem(e, formats, unlinkedURLCounts)).ToList(),
                Page = result.Page,
                PageSize = result.PageSize,
                TotalCount = result.TotalCount
            };
        }

        public async Task<SaveResult> UpdateAsync(Event tournamentEvent, CancellationToken cancellationToken = default)
        {
            if (tournamentEvent is null) throw new ArgumentNullException(nameof(tournamentEvent));

            var normalized = EventRules.Normalize(tournamentEvent);

            var errors = EventRules.Validate(normalized);
            if (errors.Count > 0)
                return SaveResult.Failure(errors);

            var updated = await _eventRepository.UpdateAsync(normalized, cancellationToken).ConfigureAwait(false);
            return updated ? SaveResult.Success() : SaveResult.Failure(["Event not found."]);
        }

        /// <summary>The event itself is left out of the count of events sharing its URL.</summary>
        /// <param name="tournamentEvent"></param>
        /// <param name="unlinkedURLCounts"></param>
        /// <returns></returns>
        private static int CountOtherUnlinkedEvents(Event tournamentEvent, IReadOnlyDictionary<string, int> unlinkedURLCounts)
        {
            if (string.IsNullOrEmpty(tournamentEvent.DecklistURL))
                return 0;

            var unlinked = unlinkedURLCounts.GetValueOrDefault(tournamentEvent.DecklistURL);
            return Math.Max(0, unlinked - (tournamentEvent.DeckID is null ? 1 : 0));
        }

        private Task<IReadOnlyDictionary<string, int>> GetUnlinkedURLCountsAsync(IEnumerable<Event> events, CancellationToken cancellationToken)
        {
            var urls = events
                .Select(e => e.DecklistURL)
                .OfType<string>()
                .Where(url => url.Length > 0)
                .Distinct()
                .ToList();

            return _eventRepository.GetUnlinkedURLCountsAsync(urls, cancellationToken);
        }

        /// <summary>
        /// Format is derived from an event's date, so filtering by format means filtering to that format's date range.
        /// Returns null when nothing can match (unknown format, or a range that misses the format entirely).
        /// </summary>
        /// <param name="criteria"></param>
        /// <param name="formats"></param>
        /// <returns></returns>
        private static EventSearchCriteria? ResolveFormatFilter(EventSearchCriteria criteria, IReadOnlyList<Format> formats)
        {
            if (criteria.FormatID is not { } formatID)
                return criteria;

            var format = formats.FirstOrDefault(f => f.ID == formatID);
            if (format is null)
                return null;

            var from = criteria.DateFrom is { } requestedFrom && requestedFrom > format.StartDate ? requestedFrom : format.StartDate;
            DateOnly? to = (criteria.DateTo, format.EndDate) switch
            {
                ({ } requestedTo, { } formatEnd) => requestedTo < formatEnd ? requestedTo : formatEnd,
                ({ } requestedTo, null) => requestedTo,
                (null, { } formatEnd) => formatEnd,
                _ => null
            };

            if (to is { } end && end < from)
                return null;

            return new EventSearchCriteria
            {
                DateFrom = from,
                DateTo = to,
                DeckName = criteria.DeckName,
                EventType = criteria.EventType,
                Location = criteria.Location,
                Page = criteria.Page,
                PageSize = criteria.PageSize
            };
        }

        private static EventListItemViewModel ToListItem(Event tournamentEvent, IReadOnlyList<Format> formats, IReadOnlyDictionary<string, int> unlinkedURLCounts) =>
            new()
            {
                Event = tournamentEvent,
                FormatName = FormatRules.FindForDate(formats, tournamentEvent.Date)?.Name ?? FormatRules.NO_FORMAT_NAME,
                OtherUnlinkedEventsWithSameURL = CountOtherUnlinkedEvents(tournamentEvent, unlinkedURLCounts),
                Record = EventRules.GetMatchRecord(tournamentEvent.Matches)
            };
    }
}
