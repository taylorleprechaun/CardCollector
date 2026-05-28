using CardCollector.Data;
using CardCollector.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace CardCollector.Repository
{
    public class CollectionRepository : ICollectionRepository
    {
        private readonly AppDbContext _context;

        public CollectionRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(CollectionEntry entry)
        {
            _context.CollectionEntries.Add(entry);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var entry = await _context.CollectionEntries.FindAsync(id);
            if (entry is null)
                return false;

            _context.CollectionEntries.Remove(entry);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ExistsAsync(int imageID, string setCode) =>
            await _context.CollectionEntries.AnyAsync(e => e.ImageID == imageID && e.SetCode == setCode);

        public async Task<IEnumerable<CollectionEntry>> GetByStatusAsync(CollectionStatus status) =>
            await _context.CollectionEntries
                .Where(e => e.Status == status)
                .OrderByDescending(e => e.DateCreated)
                .ToListAsync();

        public async Task<IEnumerable<int>> GetCollectedImageIDsAsync() =>
            await _context.CollectionEntries
                .Select(e => e.ImageID)
                .Distinct()
                .ToListAsync();

        public async Task<HashSet<int>> GetPlaceholderCardIDsAsync(IEnumerable<int> cardIDs)
        {
            var ids = cardIDs.ToHashSet();
            if (ids.Count == 0)
                return [];

            var result = await _context.CollectionEntries
                .Where(e => ids.Contains(e.CardID) && e.Status == CollectionStatus.Owned)
                .GroupBy(e => e.CardID)
                .Where(g => !g.Any(e => !e.IsPlaceholder))
                .Select(g => g.Key)
                .ToListAsync();

            return result.ToHashSet();
        }

        public async Task<Dictionary<int, CollectionStatus>> GetStatusByCardIDsAsync(IEnumerable<int> cardIDs)
        {
            var ids = cardIDs.ToHashSet();
            if (ids.Count == 0)
                return [];

            var rows = await _context.CollectionEntries
                .Where(e => ids.Contains(e.CardID))
                .GroupBy(e => e.CardID)
                .Select(g => new
                {
                    CardID = g.Key,
                    Status = g.Any(e => e.Status == CollectionStatus.Owned)
                               ? CollectionStatus.Owned
                               : CollectionStatus.Ordered
                })
                .ToListAsync();

            return rows.ToDictionary(r => r.CardID, r => r.Status);
        }

        public async Task<bool> UpdateAsync(CollectionEntry entry)
        {
            var existing = await _context.CollectionEntries.FindAsync(entry.ID);
            if (existing is null)
                return false;

            existing.AcquisitionMethod = entry.AcquisitionMethod;
            existing.Condition = entry.Condition;
            existing.Edition = entry.Edition;
            existing.IsPlaceholder = entry.IsPlaceholder;
            existing.PurchaseDate = entry.PurchaseDate;
            existing.PurchasePrice = entry.PurchasePrice;
            existing.Quantity = entry.Quantity;
            existing.DateModified = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UpdateStatusAsync(int id, CollectionStatus status, int? quantity = null)
        {
            var entry = await _context.CollectionEntries.FindAsync(id);
            if (entry is null)
                return false;

            entry.Status = status;
            entry.DateModified = DateTime.UtcNow;
            if (quantity.HasValue)
                entry.Quantity = quantity.Value;
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
