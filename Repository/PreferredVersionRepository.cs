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

        public async Task AddOrUpdateAsync(int cardID, int imageID, string setCode, string? rarityName = null)
        {
            rarityName = RarityExtensions.NormalizeRarityName(rarityName);

            var existing = await _context.PreferredVersions
                .FirstOrDefaultAsync(pv => pv.CardID == cardID)
                .ConfigureAwait(false);

            if (existing is null)
            {
                _context.PreferredVersions.Add(new PreferredVersion
                {
                    CardID = cardID,
                    ImageID = imageID,
                    RarityName = rarityName,
                    SetCode = setCode,
                    DateCreated = DateTime.UtcNow,
                    DateModified = DateTime.UtcNow
                });
            }
            else
            {
                existing.ImageID = imageID;
                existing.RarityName = rarityName;
                existing.SetCode = setCode;
                existing.DateModified = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task DeleteAsync(int imageID)
        {
            var entity = await _context.PreferredVersions
                .FirstOrDefaultAsync(pv => pv.ImageID == imageID)
                .ConfigureAwait(false);

            if (entity is not null)
            {
                _context.PreferredVersions.Remove(entity);
                await _context.SaveChangesAsync().ConfigureAwait(false);
            }
        }

        public async Task<IEnumerable<PreferredVersion>> GetAllAsync() =>
            await _context.PreferredVersions.ToListAsync().ConfigureAwait(false);

        public async Task<PreferredVersion?> GetByCardIDAsync(int cardID) =>
                    await _context.PreferredVersions
                .FirstOrDefaultAsync(pv => pv.CardID == cardID)
                .ConfigureAwait(false);
        public async Task<IReadOnlyDictionary<int, PreferredVersion>> GetByImageIDsAsync(IEnumerable<int> imageIDs)
        {
            var ids = imageIDs.ToHashSet();
            if (ids.Count == 0)
                return new Dictionary<int, PreferredVersion>();

            // Filter in memory, not in SQL: imageIDs can cover the whole owned collection (thousands of rows).
            var all = await _context.PreferredVersions
                .ToListAsync()
                .ConfigureAwait(false);

            return all.Where(pv => ids.Contains(pv.ImageID)).ToDictionary(pv => pv.ImageID);
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

    }
}
