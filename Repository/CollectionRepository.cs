using CardCollector.Data;
using CardCollector.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace CardCollector.Repository
{
    public sealed class CollectionRepository : ICollectionRepository
    {
        private readonly AppDBContext _context;

        public CollectionRepository(AppDBContext context)
        {
            _context = context;
        }

        public async Task AddAsync(CollectionEntry entry)
        {
            _context.CollectionEntries.Add(entry);
            await _context.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var entry = await _context.CollectionEntries.FindAsync(id).ConfigureAwait(false);
            if (entry is null)
                return false;

            _context.CollectionEntries.Remove(entry);
            await _context.SaveChangesAsync().ConfigureAwait(false);
            return true;
        }

        public async Task<bool> ExistsAsync(int imageID, string setCode) =>
            await _context.CollectionEntries.AnyAsync(e => e.ImageID == imageID && e.SetCode == setCode).ConfigureAwait(false);

        public async Task<IEnumerable<CollectionEntry>> GetByStatusAsync(CollectionStatus status) =>
            await _context.CollectionEntries
                .Where(e => e.Status == status)
                .OrderByDescending(e => e.DateCreated)
                .ToListAsync()
                .ConfigureAwait(false);

        public async Task<IReadOnlySet<(int ImageID, string SetCode)>> GetCollectedPairsAsync()
        {
            var pairs = await _context.CollectionEntries
                .Select(e => new { e.ImageID, e.SetCode })
                .Distinct()
                .ToListAsync()
                .ConfigureAwait(false);

            return pairs.Select(p => (p.ImageID, p.SetCode)).ToHashSet();
        }

        public async Task<OwnedCollectionStats> GetOwnedStatsAsync()
        {
            var entries = await _context.CollectionEntries
                .Where(e => e.Status == CollectionStatus.Owned)
                .Select(e => new { e.CardID, e.AcquisitionMethod, e.Quantity, e.MarketPriceAtEntry, e.PurchasePrice })
                .ToListAsync()
                .ConfigureAwait(false);

            var totalQuantity = entries.Sum(e => e.Quantity);

            var marketEntries = entries.Where(e => e.MarketPriceAtEntry.HasValue).ToList();
            decimal? marketValueAtEntry = marketEntries.Count > 0
                ? marketEntries.Sum(e => e.MarketPriceAtEntry!.Value * e.Quantity)
                : null;

            var spentEntries = entries.Where(e => e.PurchasePrice.HasValue).ToList();
            decimal? totalSpent = spentEntries.Count > 0
                ? spentEntries.Sum(e => e.PurchasePrice!.Value * e.Quantity)
                : null;

            return new OwnedCollectionStats(totalQuantity, marketValueAtEntry, totalSpent);
        }

        public async Task<IReadOnlySet<int>> GetPlaceholderCardIDsAsync(IEnumerable<int> cardIDs)
        {
            var ids = cardIDs.ToHashSet();
            if (ids.Count == 0)
                return new HashSet<int>();

            var preferredLookup = await GetPreferredPairLookupAsync().ConfigureAwait(false);

            var ownedEntries = await _context.CollectionEntries
                .Where(e => ids.Contains(e.CardID) && e.Status == CollectionStatus.Owned)
                .Select(e => new { e.CardID, e.ImageID, e.SetCode })
                .ToListAsync()
                .ConfigureAwait(false);

            var nonPlaceholderCardIDs = ownedEntries
                .Where(e => preferredLookup.Contains($"{e.ImageID}:{e.SetCode}"))
                .Select(e => e.CardID)
                .ToHashSet();

            var allOwnedCardIDs = ownedEntries.Select(e => e.CardID).ToHashSet();
            return allOwnedCardIDs.Except(nonPlaceholderCardIDs).ToHashSet();
        }

        public async Task<IReadOnlySet<int>> GetPlaceholderImageIDsAsync(IEnumerable<int> imageIDs)
        {
            var ids = imageIDs.ToHashSet();
            if (ids.Count == 0)
                return new HashSet<int>();

            var preferredLookup = await GetPreferredPairLookupAsync().ConfigureAwait(false);

            var ownedEntries = await _context.CollectionEntries
                .Where(e => ids.Contains(e.ImageID) && e.Status == CollectionStatus.Owned)
                .Select(e => new { e.ImageID, e.SetCode })
                .ToListAsync()
                .ConfigureAwait(false);

            var nonPlaceholderImageIDs = ownedEntries
                .Where(e => preferredLookup.Contains($"{e.ImageID}:{e.SetCode}"))
                .Select(e => e.ImageID)
                .ToHashSet();

            var allOwnedImageIDs = ownedEntries.Select(e => e.ImageID).ToHashSet();
            return allOwnedImageIDs.Except(nonPlaceholderImageIDs).ToHashSet();
        }

        public async Task<IReadOnlyDictionary<int, CollectionStatus>> GetStatusByCardIDsAsync(IEnumerable<int> cardIDs)
        {
            var ids = cardIDs.ToHashSet();
            if (ids.Count == 0)
                return new Dictionary<int, CollectionStatus>();

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
                .ToListAsync()
                .ConfigureAwait(false);

            return rows.ToDictionary(r => r.CardID, r => r.Status);
        }

        public async Task<IReadOnlyDictionary<int, CollectionStatus>> GetStatusByImageIDsAsync(IEnumerable<int> imageIDs)
        {
            var ids = imageIDs.ToHashSet();
            if (ids.Count == 0)
                return new Dictionary<int, CollectionStatus>();

            var rows = await _context.CollectionEntries
                .Where(e => ids.Contains(e.ImageID))
                .GroupBy(e => e.ImageID)
                .Select(g => new
                {
                    ImageID = g.Key,
                    Status = g.Any(e => e.Status == CollectionStatus.Owned)
                               ? CollectionStatus.Owned
                               : CollectionStatus.Ordered
                })
                .ToListAsync()
                .ConfigureAwait(false);

            return rows.ToDictionary(r => r.ImageID, r => r.Status);
        }

        public async Task<bool> UpdateAsync(CollectionEntry entry)
        {
            var existing = await _context.CollectionEntries.FindAsync(entry.ID).ConfigureAwait(false);
            if (existing is null)
                return false;

            existing.AcquisitionMethod = entry.AcquisitionMethod;
            existing.Condition = entry.Condition;
            existing.Edition = entry.Edition;
            existing.PurchaseDate = entry.PurchaseDate;
            existing.PurchasePrice = entry.PurchasePrice;
            existing.Quantity = entry.Quantity;
            existing.RarityName = entry.RarityName;
            existing.DateModified = DateTime.UtcNow;
            await _context.SaveChangesAsync().ConfigureAwait(false);
            return true;
        }

        public async Task<bool> UpdateStatusAsync(int id, CollectionStatus status, int? quantity = null)
        {
            var entry = await _context.CollectionEntries.FindAsync(id).ConfigureAwait(false);
            if (entry is null)
                return false;

            entry.Status = status;
            entry.DateModified = DateTime.UtcNow;
            if (quantity.HasValue)
                entry.Quantity = quantity.Value;
            await _context.SaveChangesAsync().ConfigureAwait(false);
            return true;
        }

        private async Task<HashSet<string>> GetPreferredPairLookupAsync()
        {
            var preferredPairs = await _context.PreferredVersions
                .Select(pv => new { pv.ImageID, pv.SetCode })
                .ToListAsync()
                .ConfigureAwait(false);
            return preferredPairs.Select(pv => $"{pv.ImageID}:{pv.SetCode}").ToHashSet();
        }
    }
}
