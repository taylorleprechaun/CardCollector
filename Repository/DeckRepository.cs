using CardCollector.Data;
using CardCollector.Data.Models;
using CardCollector.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CardCollector.Repository
{
    public sealed class DeckRepository : IDeckRepository
    {
        private readonly AppDBContext _context;

        public DeckRepository(AppDBContext context)
        {
            _context = context;
        }

        public async Task<int> AddAsync(Deck deck, CancellationToken cancellationToken = default)
        {
            if (deck is null) throw new ArgumentNullException(nameof(deck));

            var now = DateTime.UtcNow;
            var entity = new Deck
            {
                Cards = deck.Cards
                    .Select(c => new DeckCard
                    {
                        CardID = c.CardID,
                        Quantity = c.Quantity,
                        Section = c.Section,
                        SortOrder = c.SortOrder
                    })
                    .ToList(),
                DateCreated = now,
                DateModified = now,
                Name = deck.Name,
                Notes = deck.Notes
            };

            _context.Decks.Add(entity);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return entity.ID;
        }

        public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            var entity = await _context.Decks
                .Include(d => d.Cards)
                .FirstOrDefaultAsync(d => d.ID == id, cancellationToken)
                .ConfigureAwait(false);

            if (entity is null)
                return false;

            // Events.DeckID has no foreign key, so nothing in the database stops it pointing at a deleted deck.
            var events = await _context.Events
                .Where(e => e.DeckID == id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var now = DateTime.UtcNow;
            foreach (var tournamentEvent in events)
            {
                tournamentEvent.DeckID = null;
                tournamentEvent.DateModified = now;
            }

            _context.Decks.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }

        public async Task<IReadOnlyList<DeckListItemViewModel>> GetAllAsync(CancellationToken cancellationToken = default) =>
            await _context.Decks
                .AsNoTracking()
                .OrderBy(d => d.Name)
                .ThenBy(d => d.ID)
                .Select(d => new DeckListItemViewModel
                {
                    EventCount = _context.Events.Count(e => e.DeckID == d.ID),
                    ExtraCount = d.Cards.Where(c => c.Section == DeckSection.Extra).Sum(c => c.Quantity),
                    ID = d.ID,
                    MainCount = d.Cards.Where(c => c.Section == DeckSection.Main).Sum(c => c.Quantity),
                    Name = d.Name,
                    Notes = d.Notes,
                    SideCount = d.Cards.Where(c => c.Section == DeckSection.Side).Sum(c => c.Quantity)
                })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        public async Task<Deck?> GetAsync(int id, bool includeCards, CancellationToken cancellationToken = default)
        {
            var query = _context.Decks.AsNoTracking();
            if (includeCards)
                query = query.Include(d => d.Cards.OrderBy(c => c.SortOrder).ThenBy(c => c.ID));

            return await query
                .FirstOrDefaultAsync(d => d.ID == id, cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task<bool> UpdateAsync(Deck deck, CancellationToken cancellationToken = default)
        {
            if (deck is null) throw new ArgumentNullException(nameof(deck));

            var entity = await _context.Decks
                .FirstOrDefaultAsync(d => d.ID == deck.ID, cancellationToken)
                .ConfigureAwait(false);

            if (entity is null)
                return false;

            entity.DateModified = DateTime.UtcNow;
            entity.Name = deck.Name;
            entity.Notes = deck.Notes;

            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
    }
}
