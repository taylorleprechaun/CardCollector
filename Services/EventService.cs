using CardCollector.Data.Models;
using CardCollector.Repository;
using CardCollector.ViewModels;

namespace CardCollector.Services
{
    public sealed class EventService : IEventService
    {
        public const string NO_FORMAT_NAME = "No format";

        private readonly IEventRepository _eventRepository;
        private readonly IFormatService _formatService;

        public EventService(IEventRepository eventRepository, IFormatService formatService)
        {
            _eventRepository = eventRepository;
            _formatService = formatService;
        }

        public async Task<EventSaveResult> AddAsync(Event tournamentEvent, CancellationToken cancellationToken = default)
        {
            if (tournamentEvent is null) throw new ArgumentNullException(nameof(tournamentEvent));

            var normalized = EventRules.Normalize(tournamentEvent);
            normalized.ID = 0;

            var errors = EventRules.Validate(normalized);
            if (errors.Count > 0)
                return EventSaveResult.Failure(errors);

            await _eventRepository.AddAsync(normalized, cancellationToken).ConfigureAwait(false);
            return EventSaveResult.Success();
        }

        public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default) =>
            _eventRepository.DeleteAsync(id, cancellationToken);

        public async Task<EventDetailViewModel?> GetAsync(int id, CancellationToken cancellationToken = default)
        {
            var tournamentEvent = await _eventRepository.GetAsync(id, includeMatches: true, cancellationToken).ConfigureAwait(false);
            if (tournamentEvent is null)
                return null;

            var formats = await _formatService.GetAllAsync(cancellationToken).ConfigureAwait(false);

            return new EventDetailViewModel
            {
                DiceRecord = EventRules.GetDiceRecord(tournamentEvent.Matches),
                Event = tournamentEvent,
                Format = FormatRules.FindForDate(formats, tournamentEvent.Date),
                GameRecord = EventRules.GetGameRecord(tournamentEvent.Matches),
                MatchRecord = EventRules.GetMatchRecord(tournamentEvent.Matches)
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

            return new PagedResult<EventListItemViewModel>
            {
                Items = result.Items.Select(e => ToListItem(e, formats)).ToList(),
                Page = result.Page,
                PageSize = result.PageSize,
                TotalCount = result.TotalCount
            };
        }

        public async Task<EventSaveResult> UpdateAsync(Event tournamentEvent, CancellationToken cancellationToken = default)
        {
            if (tournamentEvent is null) throw new ArgumentNullException(nameof(tournamentEvent));

            var normalized = EventRules.Normalize(tournamentEvent);

            var errors = EventRules.Validate(normalized);
            if (errors.Count > 0)
                return EventSaveResult.Failure(errors);

            var updated = await _eventRepository.UpdateAsync(normalized, cancellationToken).ConfigureAwait(false);
            return updated ? EventSaveResult.Success() : EventSaveResult.Failure(["Event not found."]);
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

        private static EventListItemViewModel ToListItem(Event tournamentEvent, IReadOnlyList<Format> formats) =>
            new()
            {
                Event = tournamentEvent,
                FormatName = FormatRules.FindForDate(formats, tournamentEvent.Date)?.Name ?? NO_FORMAT_NAME,
                Record = EventRules.GetMatchRecord(tournamentEvent.Matches)
            };
    }
}
