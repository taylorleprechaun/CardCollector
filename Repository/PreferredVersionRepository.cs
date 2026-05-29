using CardCollector.Data;
using CardCollector.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace CardCollector.Repository
{
    public class PreferredVersionRepository : IPreferredVersionRepository
    {
        private readonly AppDbContext _context;

        public PreferredVersionRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task DeleteAsync(int imageID)
        {
            var entity = await _context.PreferredVersions.FirstOrDefaultAsync(pv => pv.ImageID == imageID);
            if (entity is not null)
            {
                _context.PreferredVersions.Remove(entity);
                await _context.SaveChangesAsync();
            }
        }

        public async Task AddOrUpdateAsync(int cardID, int imageID, string setCode)
        {
            var existing = await _context.PreferredVersions
                .FirstOrDefaultAsync(pv => pv.ImageID == imageID);

            if (existing is null)
            {
                _context.PreferredVersions.Add(new PreferredVersion
                {
                    CardID = cardID,
                    ImageID = imageID,
                    SetCode = setCode,
                    DateCreated = DateTime.UtcNow,
                    DateModified = DateTime.UtcNow
                });
            }
            else
            {
                existing.CardID = cardID;
                existing.SetCode = setCode;
                existing.DateModified = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<PreferredVersion>> GetAllAsync() =>
            await _context.PreferredVersions.ToListAsync();

        public async Task<Dictionary<int, PreferredVersion>> GetByImageIDsAsync(IEnumerable<int> imageIDs)
        {
            var ids = imageIDs.ToHashSet();
            if (ids.Count == 0)
                return [];

            var results = await _context.PreferredVersions
                .Where(pv => ids.Contains(pv.ImageID))
                .ToListAsync();

            return results.ToDictionary(pv => pv.ImageID);
        }

        public async Task<HashSet<int>> GetPreferredImageIDsAsync()
        {
            var ids = await _context.PreferredVersions
                .Select(pv => pv.ImageID)
                .ToListAsync();

            return ids.ToHashSet();
        }
    }
}
