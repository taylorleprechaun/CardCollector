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
            var latestDate = await _context.CollectionEntryValueSnapshots
                .Select(s => s.SnapshotDate)
                .OrderByDescending(d => d)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);

            if (latestDate is null)
                return [];

            return await _context.CollectionEntryValueSnapshots
                .Where(s => s.SnapshotDate == latestDate)
                .ToListAsync()
                .ConfigureAwait(false);
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
