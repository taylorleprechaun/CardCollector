using CardCollector.Data;
using CardCollector.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace CardCollector.Repository
{
    public sealed class PendingOrderRepository : IPendingOrderRepository
    {
        private readonly AppDBContext _context;

        public PendingOrderRepository(AppDBContext context)
        {
            _context = context;
        }

        public async Task AddRangeAsync(IEnumerable<PendingOrderLine> lines)
        {
            _context.PendingOrderLines.AddRange(lines);
            await _context.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var line = await _context.PendingOrderLines.FindAsync(id).ConfigureAwait(false);
            if (line is null)
                return false;

            _context.PendingOrderLines.Remove(line);
            await _context.SaveChangesAsync().ConfigureAwait(false);
            return true;
        }

        public async Task DeleteAllAsync()
        {
            _context.PendingOrderLines.RemoveRange(_context.PendingOrderLines);
            await _context.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<PendingOrderLine>> GetAllAsync() =>
            await _context.PendingOrderLines
                .OrderByDescending(l => l.DateCreated)
                .ToListAsync()
                .ConfigureAwait(false);

        public async Task<IReadOnlySet<(int ImageID, string SetCode)>> GetStagedPairsAsync()
        {
            var pairs = await _context.PendingOrderLines
                .Select(l => new { l.ImageID, l.SetCode })
                .Distinct()
                .ToListAsync()
                .ConfigureAwait(false);

            return pairs.Select(p => (p.ImageID, p.SetCode)).ToHashSet();
        }

        public async Task<(int Count, decimal Total)> GetSummaryAsync()
        {
            var lines = await _context.PendingOrderLines
                .Select(l => new { l.PurchasePrice, l.Quantity })
                .ToListAsync()
                .ConfigureAwait(false);

            var total = lines.Sum(l => (l.PurchasePrice ?? 0) * l.Quantity);
            return (lines.Count, total);
        }
    }
}
