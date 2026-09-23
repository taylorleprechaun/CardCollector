using CardCollector.Data;
using CardCollector.Data.Models;
using CardCollector.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CardCollector.Repository
{
    public sealed class EventRepository : IEventRepository
    {
        private const int DEFAULT_PAGE_SIZE = 25;
        private const string LIKE_ESCAPE = "\\";
        private const int MAX_PAGE_SIZE = 100;

        private readonly AppDBContext _context;

        public EventRepository(AppDBContext context)
        {
            _context = context;
        }

        public async Task<int> AddAsync(Event tournamentEvent, CancellationToken cancellationToken = default)
        {
            if (tournamentEvent is null) throw new ArgumentNullException(nameof(tournamentEvent));

            var now = DateTime.UtcNow;
            var entity = new Event
            {
                DateCreated = now,
                DateModified = now
            };
            CopyFields(tournamentEvent, entity);

            _context.Events.Add(entity);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return entity.ID;
        }

        public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            var entity = await _context.Events
                .Include(e => e.Matches)
                .FirstOrDefaultAsync(e => e.ID == id, cancellationToken)
                .ConfigureAwait(false);

            if (entity is null)
                return false;

            _context.Events.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }

        public async Task<IReadOnlyList<Event>> GetAllWithMatchesAsync(CancellationToken cancellationToken = default) =>
            await _context.Events
                .AsNoTracking()
                .Include(e => e.Matches.OrderBy(m => m.Sequence).ThenBy(m => m.ID))
                .OrderByDescending(e => e.Date)
                .ThenByDescending(e => e.ID)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        public async Task<Event?> GetAsync(int id, bool includeMatches, CancellationToken cancellationToken = default)
        {
            var query = _context.Events.AsNoTracking();
            if (includeMatches)
                query = query.Include(e => e.Matches.OrderBy(m => m.Sequence).ThenBy(m => m.ID));

            return await query
                .FirstOrDefaultAsync(e => e.ID == id, cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<Event>> GetByDeckAsync(int deckID, CancellationToken cancellationToken = default) =>
            await _context.Events
                .AsNoTracking()
                .Where(e => e.DeckID == deckID)
                .OrderByDescending(e => e.Date)
                .ThenByDescending(e => e.ID)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        public async Task<IReadOnlyList<string>> GetDeckNamesAsync(CancellationToken cancellationToken = default) =>
            await _context.Events
                .AsNoTracking()
                .Select(e => e.DeckName)
                .Distinct()
                .OrderBy(name => name)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        public async Task<IReadOnlyList<string>> GetLocationsAsync(CancellationToken cancellationToken = default) =>
            await _context.Events
                .AsNoTracking()
                .Select(e => e.Location)
                .Distinct()
                .OrderBy(location => location)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        public async Task<IReadOnlyList<Event>> GetUnlinkedByDecklistURLAsync(string decklistURL, int excludeEventID, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(decklistURL)) throw new ArgumentException("A decklist URL is required.", nameof(decklistURL));

            return await _context.Events
                .AsNoTracking()
                .Where(e => e.DeckID == null && e.DecklistURL == decklistURL && e.ID != excludeEventID)
                .OrderBy(e => e.Date)
                .ThenBy(e => e.ID)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task<IReadOnlyDictionary<string, int>> GetUnlinkedURLCountsAsync(IReadOnlyCollection<string> decklistURLs, CancellationToken cancellationToken = default)
        {
            if (decklistURLs is null) throw new ArgumentNullException(nameof(decklistURLs));

            var urls = decklistURLs.Distinct().ToList();
            if (urls.Count == 0)
                return new Dictionary<string, int>();

            return await _context.Events
                .AsNoTracking()
                .Where(e => e.DeckID == null && e.DecklistURL != null && urls.Contains(e.DecklistURL))
                .GroupBy(e => e.DecklistURL!)
                .Select(g => new { URL = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.URL, x => x.Count, cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task<PagedResult<Event>> SearchAsync(EventSearchCriteria criteria, CancellationToken cancellationToken = default)
        {
            if (criteria is null) throw new ArgumentNullException(nameof(criteria));

            var page = Math.Max(1, criteria.Page);
            var pageSize = criteria.PageSize is < 1 or > MAX_PAGE_SIZE ? DEFAULT_PAGE_SIZE : criteria.PageSize;

            var query = ApplyFilters(_context.Events.AsNoTracking(), criteria);

            var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);
            var items = await query
                .Include(e => e.Matches.OrderBy(m => m.Sequence).ThenBy(m => m.ID))
                .OrderByDescending(e => e.Date)
                .ThenByDescending(e => e.ID)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return new PagedResult<Event>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public async Task<int> SetDeckAsync(IReadOnlyCollection<int> eventIDs, int? deckID, CancellationToken cancellationToken = default)
        {
            if (eventIDs is null) throw new ArgumentNullException(nameof(eventIDs));

            var ids = eventIDs.Distinct().ToList();
            if (ids.Count == 0)
                return 0;

            var events = await _context.Events
                .Where(e => ids.Contains(e.ID))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var now = DateTime.UtcNow;
            foreach (var tournamentEvent in events)
            {
                tournamentEvent.DeckID = deckID;
                tournamentEvent.DateModified = now;
            }

            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return events.Count;
        }

        public async Task<bool> UpdateAsync(Event tournamentEvent, CancellationToken cancellationToken = default)
        {
            if (tournamentEvent is null) throw new ArgumentNullException(nameof(tournamentEvent));

            var entity = await _context.Events
                .FirstOrDefaultAsync(e => e.ID == tournamentEvent.ID, cancellationToken)
                .ConfigureAwait(false);

            if (entity is null)
                return false;

            CopyFields(tournamentEvent, entity);
            entity.DateModified = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }

        private static IQueryable<Event> ApplyFilters(IQueryable<Event> query, EventSearchCriteria criteria)
        {
            if (criteria.DateFrom is { } from)
                query = query.Where(e => e.Date >= from);

            if (criteria.DateTo is { } to)
                query = query.Where(e => e.Date <= to);

            if (criteria.EventType is { } eventType)
                query = query.Where(e => e.EventType == eventType);

            if (!string.IsNullOrWhiteSpace(criteria.Location))
            {
                var pattern = ContainsPattern(criteria.Location);
                query = query.Where(e => EF.Functions.Like(e.Location, pattern, LIKE_ESCAPE));
            }

            if (!string.IsNullOrWhiteSpace(criteria.DeckName))
            {
                var pattern = ContainsPattern(criteria.DeckName);
                query = query.Where(e => EF.Functions.Like(e.DeckName, pattern, LIKE_ESCAPE));
            }

            return query;
        }

        private static string ContainsPattern(string text)
        {
            var escaped = text.Trim()
                .Replace(LIKE_ESCAPE, LIKE_ESCAPE + LIKE_ESCAPE)
                .Replace("%", LIKE_ESCAPE + "%")
                .Replace("_", LIKE_ESCAPE + "_");
            return $"%{escaped}%";
        }

        /// <summary>
        /// DeckID and the rounds are managed elsewhere, so a form-driven update must not overwrite them.
        /// </summary>
        private static void CopyFields(Event source, Event target)
        {
            target.Date = source.Date;
            target.DeckName = source.DeckName;
            target.DecklistURL = source.DecklistURL;
            target.EventType = source.EventType;
            target.Finish = source.Finish;
            target.FinishNote = source.FinishNote;
            target.Location = source.Location;
            target.Notes = source.Notes;
            target.Players = source.Players;
            target.TopCut = source.TopCut;
        }
    }
}
