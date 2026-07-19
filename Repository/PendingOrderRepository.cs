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

        public async Task DeleteRangeAsync(IEnumerable<int> ids)
        {
            var idSet = ids.ToHashSet();
            if (idSet.Count == 0)
                return;

            var lines = await _context.PendingOrderLines
                .Where(l => idSet.Contains(l.ID))
                .ToListAsync()
                .ConfigureAwait(false);

            _context.PendingOrderLines.RemoveRange(lines);
            await _context.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<PendingOrderLine>> GetAllAsync() =>
            await _context.PendingOrderLines
                .OrderByDescending(l => l.DateCreated)
                .ToListAsync()
                .ConfigureAwait(false);

        public async Task<IReadOnlyList<PendingOrderLine>> GetByIDsAsync(IEnumerable<int> ids)
        {
            var idSet = ids.ToHashSet();
            if (idSet.Count == 0)
                return [];

            return await _context.PendingOrderLines
                .Where(l => idSet.Contains(l.ID))
                .OrderByDescending(l => l.DateCreated)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        public async Task<IReadOnlyDictionary<(int ImageID, string SetCode, string RarityName), int>> GetStagedQuantitiesAsync()
        {
            var grouped = await _context.PendingOrderLines
                .GroupBy(l => new { l.ImageID, l.SetCode, RarityName = l.RarityName ?? string.Empty })
                .Select(g => new { g.Key.ImageID, g.Key.SetCode, g.Key.RarityName, Quantity = g.Sum(l => l.Quantity) })
                .ToListAsync()
                .ConfigureAwait(false);

            return grouped.ToDictionary(g => (g.ImageID, g.SetCode, g.RarityName), g => g.Quantity);
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
