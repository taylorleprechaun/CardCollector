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

        public async Task<CheckedOutCard?> GetAsync(int printingID) =>
            await _context.CheckedOutCards
                .FirstOrDefaultAsync(c => c.CatalogPrintingID == printingID)
                .ConfigureAwait(false);

        public async Task<IReadOnlyDictionary<int, (DateTime Date, int Quantity)>> GetCheckedOutLookupAsync()
        {
            var records = await _context.CheckedOutCards
                .Where(c => c.CatalogPrintingID != null)
                .Select(c => new { PrintingID = c.CatalogPrintingID!.Value, c.CheckedOutDate, c.Quantity })
                .ToListAsync()
                .ConfigureAwait(false);

            return records.ToDictionary(r => r.PrintingID, r => (r.CheckedOutDate, r.Quantity));
        }

        public async Task<bool> RemoveAsync(int printingID)
        {
            var entry = await GetAsync(printingID).ConfigureAwait(false);
            if (entry is null)
                return false;

            _context.CheckedOutCards.Remove(entry);
            await _context.SaveChangesAsync().ConfigureAwait(false);
            return true;
        }

        public async Task UpdateAsync(int printingID, int quantity)
        {
            var entry = await GetAsync(printingID).ConfigureAwait(false);
            if (entry is null)
                return;

            entry.Quantity = quantity;
            entry.DateModified = DateTime.UtcNow;
            await _context.SaveChangesAsync().ConfigureAwait(false);
        }
    }
}
