using CardCollector.Data;
using CardCollector.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace CardCollector.Repository
{
    public sealed class IgnoredCardRepository : IIgnoredCardRepository
    {
        private readonly AppDBContext _context;

        public IgnoredCardRepository(AppDBContext context)
        {
            _context = context;
        }

        public async Task AddAsync(int cardID)
        {
            var exists = await _context.IgnoredCards
                .AnyAsync(i => i.CardID == cardID)
                .ConfigureAwait(false);

            if (exists)
                return;

            _context.IgnoredCards.Add(new IgnoredCard
            {
                CardID = cardID,
                DateCreated = DateTime.UtcNow,
                DateModified = DateTime.UtcNow
            });

            await _context.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task<IReadOnlyDictionary<int, DateTime>> GetAllAsync()
        {
            var rows = await _context.IgnoredCards
                .Select(i => new { i.CardID, i.DateCreated })
                .ToListAsync()
                .ConfigureAwait(false);

            return rows.ToDictionary(r => r.CardID, r => r.DateCreated);
        }

        public async Task<IReadOnlySet<int>> GetIgnoredCardIDsAsync()
        {
            var ids = await _context.IgnoredCards
                .Select(i => i.CardID)
                .ToListAsync()
                .ConfigureAwait(false);

            return ids.ToHashSet();
        }

        public async Task<bool> IsIgnoredAsync(int cardID) =>
            await _context.IgnoredCards
                .AnyAsync(i => i.CardID == cardID)
                .ConfigureAwait(false);

        public async Task RemoveAsync(int cardID)
        {
            var entity = await _context.IgnoredCards
                .FirstOrDefaultAsync(i => i.CardID == cardID)
                .ConfigureAwait(false);

            if (entity is not null)
            {
                _context.IgnoredCards.Remove(entity);
                await _context.SaveChangesAsync().ConfigureAwait(false);
            }
        }
    }
}
