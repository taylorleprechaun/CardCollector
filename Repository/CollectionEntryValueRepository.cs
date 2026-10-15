using CardCollector.Data;
using CardCollector.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace CardCollector.Repository
{
    public sealed class CollectionEntryValueRepository : ICollectionEntryValueRepository
    {
        private readonly AppDBContext _context;

        public CollectionEntryValueRepository(AppDBContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlySet<int>> GetCatalogCardIDsAsync()
        {
            var ids = await _context.CollectionEntryValueSnapshots
                .Where(s => s.CatalogCardID != null)
                .Select(s => s.CatalogCardID!.Value)
                .Distinct()
                .ToListAsync()
                .ConfigureAwait(false);
            return ids.ToHashSet();
        }

        public async Task<IEnumerable<CollectionEntryValueSnapshot>> GetHistoryByCardIDAsync(int cardID)
        {
            return await _context.CollectionEntryValueSnapshots
                .Where(s => s.CatalogCardID == cardID)
                .OrderBy(s => s.SnapshotDate)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        public async Task<IEnumerable<CollectionEntryValueSnapshot>> GetLatestSnapshotsAsync()
        {
            var subquery = _context.CollectionEntryValueSnapshots
                .GroupBy(s => s.CollectionEntryID)
                .Select(g => new { CollectionEntryID = g.Key, MaxDate = g.Max(s => s.SnapshotDate) });

            return await (
                from s in _context.CollectionEntryValueSnapshots
                join q in subquery
                    on new { s.CollectionEntryID, s.SnapshotDate }
                    equals new { q.CollectionEntryID, SnapshotDate = q.MaxDate }
                select s
            ).ToListAsync().ConfigureAwait(false);
        }

        public async Task PruneSnapshotsAsync()
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-30).ToString("yyyy-MM-dd");
            await _context.Database.ExecuteSqlAsync(
                $"""
                DELETE FROM CollectionEntryValueSnapshots
                WHERE SnapshotDate < {cutoffDate}
                  AND SnapshotDate NOT IN (
                      SELECT MAX(SnapshotDate)
                      FROM CollectionEntryValueSnapshots
                      WHERE SnapshotDate < {cutoffDate}
                      GROUP BY substr(SnapshotDate, 1, 7)
                  )
                """).ConfigureAwait(false);
        }
        
        public async Task UpsertSnapshotsAsync(IEnumerable<CollectionEntryValueSnapshot> snapshots, string snapshotDate)
        {
            var existing = await _context.CollectionEntryValueSnapshots
                .Where(s => s.SnapshotDate == snapshotDate)
                .ToListAsync()
                .ConfigureAwait(false);

            _context.CollectionEntryValueSnapshots.RemoveRange(existing);
            _context.CollectionEntryValueSnapshots.AddRange(snapshots);

            await _context.SaveChangesAsync().ConfigureAwait(false);
        }
    }
}
