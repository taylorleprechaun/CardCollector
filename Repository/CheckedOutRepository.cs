using CardCollector.Data;
using CardCollector.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace CardCollector.Repository
{
    public sealed class CheckedOutRepository : ICheckedOutRepository
    {
        private readonly AppDBContext _context;

        public CheckedOutRepository(AppDBContext context)
        {
            _context = context;
        }

        public async Task AddAsync(CheckedOutCard entry)
        {
            if (entry is null) throw new ArgumentNullException(nameof(entry));

            _context.CheckedOutCards.Add(entry);
            await _context.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<CheckedOutCard>> GetAllAsync() =>
            await _context.CheckedOutCards
                .OrderByDescending(c => c.CheckedOutDate)
                .ToListAsync()
                .ConfigureAwait(false);

        public async Task<CheckedOutCard?> GetAsync(int imageID, string setCode) =>
            await _context.CheckedOutCards
                .FirstOrDefaultAsync(c => c.ImageID == imageID && c.SetCode == setCode)
                .ConfigureAwait(false);

        public async Task<IReadOnlyDictionary<(int ImageID, string SetCode), (DateTime Date, int Quantity)>> GetCheckedOutLookupAsync()
        {
            var records = await _context.CheckedOutCards
                .Select(c => new { c.ImageID, c.SetCode, c.CheckedOutDate, c.Quantity })
                .ToListAsync()
                .ConfigureAwait(false);

            return records.ToDictionary(r => (r.ImageID, r.SetCode), r => (r.CheckedOutDate, r.Quantity));
        }

        public async Task UpdateAsync(int imageID, string setCode, int quantity)
        {
            var entry = await _context.CheckedOutCards
                .FirstOrDefaultAsync(c => c.ImageID == imageID && c.SetCode == setCode)
                .ConfigureAwait(false);

            if (entry is null)
                return;

            entry.Quantity = quantity;
            entry.DateModified = DateTime.UtcNow;
            await _context.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task<bool> RemoveAsync(int imageID, string setCode)
        {
            var entry = await _context.CheckedOutCards
                .FirstOrDefaultAsync(c => c.ImageID == imageID && c.SetCode == setCode)
                .ConfigureAwait(false);

            if (entry is null)
                return false;

            _context.CheckedOutCards.Remove(entry);
            await _context.SaveChangesAsync().ConfigureAwait(false);
            return true;
        }
    }
}
