using CardCollector.Data;
using CardCollector.Data.Models;
using CardCollector.DTO;
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

        public async Task AddOrUpdateAsync(int cardID, string setCode, string? rarityName = null, string? printVariant = null, int? desiredQuantity = null)
        {
            rarityName = RarityExtensions.NormalizeRarityName(rarityName);

            var existing = await _context.PreferredVersions
                .FirstOrDefaultAsync(pv => pv.CardID == cardID && pv.SetCode == setCode && pv.RarityName == rarityName && pv.PrintVariant == printVariant)
                .ConfigureAwait(false);

            if (existing is null)
            {
                _context.PreferredVersions.Add(new PreferredVersion
                {
                    CardID = cardID,
                    DesiredQuantity = desiredQuantity ?? 3,
                    PrintVariant = printVariant,
                    RarityName = rarityName,
                    SetCode = setCode,
                    DateCreated = DateTime.UtcNow,
                    DateModified = DateTime.UtcNow
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
                .Where(pv => pv.CardID == cardID)
                .ToListAsync()
                .ConfigureAwait(false);

        public async Task<IReadOnlyDictionary<int, IReadOnlyList<PreferredVersion>>> GetByCardIDsAsync(IEnumerable<int> cardIDs)
        {
            var ids = cardIDs.ToHashSet();
            if (ids.Count == 0)
                return new Dictionary<int, IReadOnlyList<PreferredVersion>>();

            // Filter in memory, not in SQL: cardIDs can cover the whole owned collection (thousands of rows).
            var all = await _context.PreferredVersions
                .ToListAsync()
                .ConfigureAwait(false);

            return all
                .Where(pv => ids.Contains(pv.CardID))
                .GroupBy(pv => pv.CardID)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<PreferredVersion>)g.ToList());
        }

        public async Task<IReadOnlySet<int>> GetPreferredCardIDsAsync()
        {
            var ids = await _context.PreferredVersions
                .Select(pv => pv.CardID)
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

        public async Task<bool> UpgradeAsync(int id, string newSetCode, string newRarityName, string? newPrintVariant = null)
        {
            var entity = await _context.PreferredVersions.FindAsync(id).ConfigureAwait(false);
            if (entity is null)
                return false;

            entity.SetCode = newSetCode;
            entity.RarityName = RarityExtensions.NormalizeRarityName(newRarityName);
            entity.PrintVariant = newPrintVariant;
            entity.DateModified = DateTime.UtcNow;
            await _context.SaveChangesAsync().ConfigureAwait(false);
            return true;
        }
    }
}
