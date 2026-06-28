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

        public async Task<IEnumerable<string>> GetDistinctCardNamesAsync()
        {
            return await _context.CollectionEntryValueSnapshots
                .Select(s => s.CardName)
                .Distinct()
                .OrderBy(n => n)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        public async Task<IEnumerable<CollectionEntryValueSnapshot>> GetHistoryByCardNameAsync(string cardName)
        {
            return await _context.CollectionEntryValueSnapshots
                .Where(s => s.CardName == cardName)
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

        public async Task UpsertSelectiveSnapshotsAsync(IEnumerable<CollectionEntryValueSnapshot> snapshots)
        {
            var snapshotList = snapshots.ToList();
            if (snapshotList.Count == 0)
                return;

            var snapshotDate = snapshotList[0].SnapshotDate;
            var entryIDs = snapshotList.Select(s => s.CollectionEntryID).ToHashSet();

            var existing = await _context.CollectionEntryValueSnapshots
                .Where(s => s.SnapshotDate == snapshotDate && entryIDs.Contains(s.CollectionEntryID))
                .ToListAsync()
                .ConfigureAwait(false);

            _context.CollectionEntryValueSnapshots.RemoveRange(existing);
            _context.CollectionEntryValueSnapshots.AddRange(snapshotList);

            await _context.SaveChangesAsync().ConfigureAwait(false);
        }
    }
}
