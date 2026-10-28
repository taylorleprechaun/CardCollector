using CardCollector.Data;
using CardCollector.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace CardCollector.Repository
{
    public sealed class PreferredVersionRepository : IPreferredVersionRepository
    {
        private readonly AppDBContext _context;

        public PreferredVersionRepository(AppDBContext context)
        {
            _context = context;
        }

        public async Task AddOrUpdateAsync(int printingID, int? desiredQuantity = null)
        {
            var existing = await _context.PreferredVersions
                .FirstOrDefaultAsync(pv => pv.CatalogPrintingID == printingID)
                .ConfigureAwait(false);

            if (existing is null)
            {
                _context.PreferredVersions.Add(new PreferredVersion
                {
                    CatalogPrintingID = printingID,
                    DateCreated = DateTime.UtcNow,
                    DateModified = DateTime.UtcNow,
                    DesiredQuantity = desiredQuantity ?? 3
                });
            }
            else
            {
                if (desiredQuantity.HasValue)
                    existing.DesiredQuantity = desiredQuantity.Value;
                existing.DateModified = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task DeleteAsync(int id)
        {
            var entity = await _context.PreferredVersions.FindAsync(id).ConfigureAwait(false);
            if (entity is not null)
            {
                _context.PreferredVersions.Remove(entity);
                await _context.SaveChangesAsync().ConfigureAwait(false);
            }
        }

        public async Task<IEnumerable<PreferredVersion>> GetAllAsync() =>
            await _context.PreferredVersions.ToListAsync().ConfigureAwait(false);

        public async Task<IReadOnlyList<PreferredVersion>> GetByCardIDAsync(int cardID) =>
            await _context.PreferredVersions
                .Where(pv => pv.CatalogPrinting!.CatalogCardID == cardID)
                .ToListAsync()
                .ConfigureAwait(false);

        public async Task<IReadOnlyDictionary<int, IReadOnlyList<PreferredVersion>>> GetByCardIDsAsync(IEnumerable<int> cardIDs)
        {
            var ids = cardIDs.ToHashSet();
            if (ids.Count == 0)
                return new Dictionary<int, IReadOnlyList<PreferredVersion>>();

            // Filter in memory, not in SQL: cardIDs can cover the whole owned collection (thousands of rows).
            // The card ID is projected in the query: the printing navigation isn't loaded once the rows are in memory.
            var all = await _context.PreferredVersions
                .Select(pv => new { CardID = pv.CatalogPrinting!.CatalogCardID, Version = pv })
                .ToListAsync()
                .ConfigureAwait(false);

            return all
                .Where(x => ids.Contains(x.CardID))
                .GroupBy(x => x.CardID)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<PreferredVersion>)g.Select(x => x.Version).ToList());
        }

        public async Task<PreferredVersion?> GetByIDAsync(int id) =>
            await _context.PreferredVersions.FindAsync(id).ConfigureAwait(false);

        public async Task<IReadOnlySet<int>> GetPreferredCardIDsAsync()
        {
            var ids = await _context.PreferredVersions
                .Select(pv => pv.CatalogPrinting!.CatalogCardID)
                .Distinct()
                .ToListAsync()
                .ConfigureAwait(false);

            return ids.ToHashSet();
        }

        public async Task<bool> UpdateDesiredQuantityAsync(int id, int desiredQuantity)
        {
            var entity = await _context.PreferredVersions.FindAsync(id).ConfigureAwait(false);
            if (entity is null)
                return false;

            entity.DesiredQuantity = desiredQuantity;
            entity.DateModified = DateTime.UtcNow;
            await _context.SaveChangesAsync().ConfigureAwait(false);
            return true;
        }

        public async Task<bool> UpgradeAsync(int id, int newPrintingID)
        {
            var entity = await _context.PreferredVersions.FindAsync(id).ConfigureAwait(false);
            if (entity is null)
                return false;
            if (entity.CatalogPrintingID == newPrintingID)
                return true;

            // A printing is tracked at most once, so moving onto one that is already tracked folds the two together.
            var existing = await _context.PreferredVersions
                .FirstOrDefaultAsync(pv => pv.CatalogPrintingID == newPrintingID)
                .ConfigureAwait(false);
            if (existing is not null)
            {
                existing.DesiredQuantity = Math.Max(existing.DesiredQuantity, entity.DesiredQuantity);
                existing.DateModified = DateTime.UtcNow;
                _context.PreferredVersions.Remove(entity);
            }
            else
            {
                entity.CatalogPrintingID = newPrintingID;
                entity.DateModified = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync().ConfigureAwait(false);
            return true;
        }
    }
}
