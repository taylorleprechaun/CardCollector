using CardCollector.Data;
using CardCollector.Data.Models;
using CardCollector.ViewModels;
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

        public async Task<IEnumerable<CollectionEntry>> GetByCardIDAsync(int cardID) =>
            await _context.CollectionEntries
                .Where(e => e.CardID == cardID)
                .ToListAsync()
                .ConfigureAwait(false);

        public async Task<CollectionEntry?> GetByIDAsync(int id) =>
            await _context.CollectionEntries.FindAsync(id).ConfigureAwait(false);

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

        public async Task<IReadOnlySet<(int ImageID, string SetCode)>> GetOwnedPairsAsync()
        {
            var pairs = await _context.CollectionEntries
                .Where(e => e.Status == CollectionStatus.Owned)
                .Select(e => new { e.ImageID, e.SetCode })
                .Distinct()
                .ToListAsync()
                .ConfigureAwait(false);

            return pairs.Select(p => (p.ImageID, p.SetCode)).ToHashSet();
        }

        public async Task<IReadOnlyDictionary<int, CollectionCompletionStatus>> GetCompletionStatusByImageIDsAsync(IEnumerable<int> imageIDs)
        {
            var ids = imageIDs.ToHashSet();
            if (ids.Count == 0)
                return new Dictionary<int, CollectionCompletionStatus>();

            var ownedEntries = await _context.CollectionEntries
                .Where(e => ids.Contains(e.ImageID) && e.Status == CollectionStatus.Owned)
                .Select(e => new { e.ImageID, e.SetCode, e.RarityName, e.Quantity })
                .ToListAsync()
                .ConfigureAwait(false);

            if (ownedEntries.Count == 0)
                return new Dictionary<int, CollectionCompletionStatus>();

            var preferredVersions = await _context.PreferredVersions
                .Where(pv => ids.Contains(pv.ImageID))
                .Select(pv => new { pv.ImageID, pv.SetCode, pv.RarityName })
                .ToListAsync()
                .ConfigureAwait(false);

            var result = new Dictionary<int, CollectionCompletionStatus>();

            foreach (var group in ownedEntries.GroupBy(e => e.ImageID))
            {
                var preferred = preferredVersions.FirstOrDefault(pv => pv.ImageID == group.Key);
                var preferredEntries = preferred is null ? [] : group.Where(e =>
                    e.SetCode.Equals(preferred.SetCode, StringComparison.OrdinalIgnoreCase) &&
                    (preferred.RarityName is null || preferred.RarityName.Equals(e.RarityName, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                if (preferredEntries.Count == 0)
                {
                    result[group.Key] = CollectionCompletionStatus.Placeholder;
                    continue;
                }

                var qty = preferredEntries.Sum(e => e.Quantity);
                result[group.Key] = qty >= CardPrinting.CompleteThreshold
                    ? CollectionCompletionStatus.Complete
                    : CollectionCompletionStatus.Incomplete;
            }

            return result;
        }

        public async Task<IReadOnlyList<AcquisitionMethod>> GetDistinctAcquisitionMethodsAsync() =>
            await _context.CollectionEntries
                .Where(e => e.Status == CollectionStatus.Owned && e.AcquisitionMethod != null)
                .Select(e => e.AcquisitionMethod!.Value)
                .Distinct()
                .OrderBy(v => v)
                .ToListAsync()
                .ConfigureAwait(false);

        public async Task<IReadOnlyList<CardCondition>> GetDistinctConditionsAsync() =>
            await _context.CollectionEntries
                .Where(e => e.Status == CollectionStatus.Owned && e.Condition != null)
                .Select(e => e.Condition!.Value)
                .Distinct()
                .OrderBy(v => v)
                .ToListAsync()
                .ConfigureAwait(false);

        public async Task<IReadOnlyList<CardEdition>> GetDistinctEditionsAsync() =>
            await _context.CollectionEntries
                .Where(e => e.Status == CollectionStatus.Owned && e.Edition != null)
                .Select(e => e.Edition!.Value)
                .Distinct()
                .OrderBy(v => v)
                .ToListAsync()
                .ConfigureAwait(false);

        public async Task<IReadOnlyList<string>> GetDistinctRarityNamesAsync() =>
            await _context.CollectionEntries
                .Where(e => e.Status == CollectionStatus.Owned && e.RarityName != null)
                .Select(e => e.RarityName!)
                .Distinct()
                .OrderBy(r => r)
                .ToListAsync()
                .ConfigureAwait(false);

        public async Task<IReadOnlyList<string>> GetDistinctSetCodesAsync() =>
            await _context.CollectionEntries
                .Where(e => e.Status == CollectionStatus.Owned)
                .Select(e => e.SetCode)
                .Distinct()
                .OrderBy(s => s)
                .ToListAsync()
                .ConfigureAwait(false);

        public async Task<IReadOnlyDictionary<(int ImageID, string SetCode, string RarityName), int>> GetOwnedQuantitiesForPairsAsync(IEnumerable<(int ImageID, string SetCode, string RarityName)> pairs)
        {
            var pairList = pairs.ToList();
            if (pairList.Count == 0)
                return new Dictionary<(int ImageID, string SetCode, string RarityName), int>();

            var imageIDs = pairList.Select(p => p.ImageID).ToHashSet();

            var entries = await _context.CollectionEntries
                .Where(e => imageIDs.Contains(e.ImageID) && e.Status == CollectionStatus.Owned)
                .Select(e => new { e.ImageID, e.SetCode, e.RarityName, e.Quantity })
                .ToListAsync()
                .ConfigureAwait(false);

            var pairSet = pairList.ToHashSet();

            return entries
                .Where(e => pairSet.Contains((e.ImageID, e.SetCode, e.RarityName ?? string.Empty)))
                .GroupBy(e => (e.ImageID, e.SetCode, RarityName: e.RarityName ?? string.Empty))
                .ToDictionary(g => g.Key, g => g.Sum(e => e.Quantity));
        }

        public async Task<IReadOnlyDictionary<(int ImageID, string SetCode), int>> GetOwnedQuantitiesForPreferredVersionsAsync(
            IEnumerable<(int ImageID, string SetCode, string? RarityName)> preferredVersions)
        {
            var pvList = preferredVersions.ToList();
            if (pvList.Count == 0)
                return new Dictionary<(int ImageID, string SetCode), int>();

            var imageIDs = pvList.Select(p => p.ImageID).ToHashSet();

            var entries = await _context.CollectionEntries
                .Where(e => imageIDs.Contains(e.ImageID) && e.Status == CollectionStatus.Owned)
                .Select(e => new { e.ImageID, e.SetCode, e.RarityName, e.Quantity })
                .ToListAsync()
                .ConfigureAwait(false);

            var result = new Dictionary<(int ImageID, string SetCode), int>();
            foreach (var pv in pvList)
            {
                var qty = entries
                    .Where(e => e.ImageID == pv.ImageID
                        && e.SetCode.Equals(pv.SetCode, StringComparison.OrdinalIgnoreCase)
                        && (pv.RarityName is null || pv.RarityName.Equals(e.RarityName, StringComparison.OrdinalIgnoreCase)))
                    .Sum(e => e.Quantity);

                if (qty > 0)
                    result[(pv.ImageID, pv.SetCode)] = qty;
            }
            return result;
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

        public async Task<IReadOnlySet<int>> GetCardIDsByStatusAsync(CollectionStatus status)
        {
            var ids = await _context.CollectionEntries
                .Where(e => e.Status == status)
                .Select(e => e.CardID)
                .Distinct()
                .ToListAsync()
                .ConfigureAwait(false);
            return ids.ToHashSet();
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
    }
}
