using CardCollector.Data;
using CardCollector.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace CardCollector.Repository
{
    public sealed class CollectionValueRepository : ICollectionValueRepository
    {
        private readonly AppDBContext _context;

        public CollectionValueRepository(AppDBContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<CollectionValueSnapshot>> GetAllSnapshotsAsync() =>
            await _context.CollectionValueSnapshots
                .OrderBy(s => s.SnapshotDate)
                .ToListAsync()
                .ConfigureAwait(false);

        public async Task<CollectionValueSnapshot?> GetLatestSnapshotAsync() =>
            await _context.CollectionValueSnapshots
                .OrderByDescending(s => s.SnapshotDate)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);
        
        public async Task PruneSnapshotsAsync()
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-30).ToString("yyyy-MM-dd");
            await _context.Database.ExecuteSqlAsync(
                $"""
                DELETE FROM CollectionValueSnapshots
                WHERE SnapshotDate < {cutoffDate}
                  AND SnapshotDate NOT IN (
                      SELECT MAX(SnapshotDate)
                      FROM CollectionValueSnapshots
                      WHERE SnapshotDate < {cutoffDate}
                      GROUP BY substr(SnapshotDate, 1, 7)
                  )
                """).ConfigureAwait(false);
        }

        public async Task UpsertSnapshotAsync(CollectionValueSnapshot snapshot)
        {
            var existing = await _context.CollectionValueSnapshots
                .FirstOrDefaultAsync(s => s.SnapshotDate == snapshot.SnapshotDate)
                .ConfigureAwait(false);

            if (existing is not null)
            {
                existing.CardCount = snapshot.CardCount;
                existing.TotalValue = snapshot.TotalValue;
            }
            else
            {
                _context.CollectionValueSnapshots.Add(snapshot);
            }

            await _context.SaveChangesAsync().ConfigureAwait(false);
        }
    }
}
