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
            if (entry is null) throw new ArgumentNullException(nameof(entry));

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
                .Where(e => e.CatalogPrinting!.CatalogCardID == cardID)
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

        public async Task<IReadOnlySet<int>> GetCardIDsByStatusAsync(CollectionStatus status)
        {
            var ids = await _context.CollectionEntries
                .Where(e => e.Status == status)
                .Select(e => e.CatalogPrinting!.CatalogCardID)
                .Distinct()
                .ToListAsync()
                .ConfigureAwait(false);
            return ids.ToHashSet();
        }

        public async Task<IReadOnlyDictionary<int, CollectionCompletionStatus>> GetCompletionStatusByCardIDsAsync(IEnumerable<int> cardIDs)
        {
            var ids = cardIDs.ToHashSet();
            if (ids.Count == 0)
                return new Dictionary<int, CollectionCompletionStatus>();

            var ownedEntries = await _context.CollectionEntries
                .Where(e => ids.Contains(e.CatalogPrinting!.CatalogCardID) && e.Status == CollectionStatus.Owned)
                .Select(e => new { CardID = e.CatalogPrinting!.CatalogCardID, e.CatalogPrintingID, e.Quantity })
                .ToListAsync()
                .ConfigureAwait(false);

            if (ownedEntries.Count == 0)
                return new Dictionary<int, CollectionCompletionStatus>();

            var trackedPrintings = await _context.PreferredVersions
                .Where(pv => ids.Contains(pv.CatalogPrinting!.CatalogCardID))
                .Select(pv => new { CardID = pv.CatalogPrinting!.CatalogCardID, pv.CatalogPrintingID, pv.DesiredQuantity })
                .ToListAsync()
                .ConfigureAwait(false);

            var result = new Dictionary<int, CollectionCompletionStatus>();

            foreach (var group in ownedEntries.GroupBy(e => e.CardID))
            {
                // A card can have more than one tracked printing (e.g. two different set/rarity printings).
                // Only printings with at least one matching owned entry count toward the roll-up; if none
                // match, this card isn't one of the tracked printings.
                var trackedForCard = trackedPrintings.Where(pv => pv.CardID == group.Key).ToList();
                var statuses = new List<CollectionCompletionStatus>();

                foreach (var tracked in trackedForCard)
                {
                    var matchingEntries = group
                        .Where(e => e.CatalogPrintingID == tracked.CatalogPrintingID)
                        .ToList();

                    if (matchingEntries.Count == 0)
                        continue;

                    var qty = matchingEntries.Sum(e => e.Quantity);
                    statuses.Add(qty >= tracked.DesiredQuantity ? CollectionCompletionStatus.Complete : CollectionCompletionStatus.Incomplete);
                }

                if (statuses.Count == 0)
                {
                    result[group.Key] = CollectionCompletionStatus.Placeholder;
                    continue;
                }

                result[group.Key] = statuses.Contains(CollectionCompletionStatus.Complete)
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

        public async Task<IReadOnlyDictionary<int, int>> GetOrderedQuantitiesAsync()
        {
            var grouped = await _context.CollectionEntries
                .Where(e => e.Status == CollectionStatus.Ordered)
                .GroupBy(e => e.CatalogPrintingID)
                .Select(g => new { PrintingID = g.Key, Quantity = g.Sum(e => e.Quantity) })
                .ToListAsync()
                .ConfigureAwait(false);

            return grouped.ToDictionary(g => g.PrintingID, g => g.Quantity);
        }

        public async Task<IReadOnlyList<(int CardID, int PrintingID)>> GetOwnedCardPrintingsAsync()
        {
            var entries = await _context.CollectionEntries
                .Where(e => e.Status == CollectionStatus.Owned)
                .Select(e => new { CardID = e.CatalogPrinting!.CatalogCardID, PrintingID = e.CatalogPrintingID })
                .Distinct()
                .ToListAsync()
                .ConfigureAwait(false);

            return entries.Select(e => (e.CardID, e.PrintingID)).ToList();
        }

        public async Task<IReadOnlyDictionary<int, int>> GetQuantitiesByPrintingAsync(IEnumerable<int> cardIDs, CollectionStatus status)
        {
            var ids = cardIDs.ToHashSet();
            if (ids.Count == 0)
                return new Dictionary<int, int>();

            var entries = await _context.CollectionEntries
                .Where(e => ids.Contains(e.CatalogPrinting!.CatalogCardID) && e.Status == status)
                .Select(e => new { PrintingID = e.CatalogPrintingID, e.Quantity })
                .ToListAsync()
                .ConfigureAwait(false);

            return entries
                .GroupBy(e => e.PrintingID)
                .ToDictionary(g => g.Key, g => g.Sum(e => e.Quantity));
        }

        public async Task<IReadOnlySet<int>> GetOwnedPrintingIDsAsync()
        {
            var ids = await _context.CollectionEntries
                .Where(e => e.Status == CollectionStatus.Owned)
                .Select(e => e.CatalogPrintingID)
                .Distinct()
                .ToListAsync()
                .ConfigureAwait(false);

            return ids.ToHashSet();
        }

        public async Task<IReadOnlyDictionary<int, int>> GetOwnedQuantitiesByPrintingAsync(IEnumerable<int> printingIDs)
        {
            var ids = printingIDs.ToHashSet();
            if (ids.Count == 0)
                return new Dictionary<int, int>();

            // Filter in memory, not in SQL: printingIDs can be every tracked printing (about 14,000), and sending them as an
            // IN list made one parameter per ID. Summing per printing in SQL keeps the result to one row per owned printing.
            var owned = await _context.CollectionEntries
                .Where(e => e.Status == CollectionStatus.Owned)
                .GroupBy(e => e.CatalogPrintingID)
                .Select(g => new { PrintingID = g.Key, Quantity = g.Sum(e => e.Quantity) })
                .ToListAsync()
                .ConfigureAwait(false);

            return owned
                .Where(o => ids.Contains(o.PrintingID))
                .ToDictionary(o => o.PrintingID, o => o.Quantity);
        }

        public async Task<OwnedCollectionStats> GetOwnedStatsAsync()
        {
            var entries = await _context.CollectionEntries
                .Where(e => e.Status == CollectionStatus.Owned)
                .Select(e => new { e.Quantity, e.MarketPriceAtEntry, e.PurchasePrice })
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

        public async Task<IReadOnlyDictionary<int, CollectionStatus>> GetStatusByCardIDsAsync(IEnumerable<int> cardIDs)
        {
            var ids = cardIDs.ToHashSet();
            if (ids.Count == 0)
                return new Dictionary<int, CollectionStatus>();

            var rows = await _context.CollectionEntries
                .Where(e => ids.Contains(e.CatalogPrinting!.CatalogCardID))
                .GroupBy(e => e.CatalogPrinting!.CatalogCardID)
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
            existing.MarketPriceAtEntry = entry.MarketPriceAtEntry;
            existing.PurchaseDate = entry.PurchaseDate;
            existing.PurchasePrice = entry.PurchasePrice;
            existing.Quantity = entry.Quantity;
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
