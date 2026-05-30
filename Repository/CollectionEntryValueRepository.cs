using CardCollector.Data;
using CardCollector.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace CardCollector.Repository
{
    public class CollectionEntryValueRepository : ICollectionEntryValueRepository
    {
        private readonly AppDBContext _context;

        public CollectionEntryValueRepository(AppDBContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<CollectionEntryValueSnapshot>> GetLatestSnapshotsAsync()
        {
            var latestDate = await _context.CollectionEntryValueSnapshots
                .Select(s => s.SnapshotDate)
                .OrderByDescending(d => d)
                .FirstOrDefaultAsync();

            if (latestDate is null)
                return [];

            return await _context.CollectionEntryValueSnapshots
                .Where(s => s.SnapshotDate == latestDate)
                .ToListAsync();
        }

        public async Task UpsertSnapshotsAsync(IEnumerable<CollectionEntryValueSnapshot> snapshots, string snapshotDate)
        {
            var existing = await _context.CollectionEntryValueSnapshots
                .Where(s => s.SnapshotDate == snapshotDate)
                .ToListAsync();

            _context.CollectionEntryValueSnapshots.RemoveRange(existing);
            _context.CollectionEntryValueSnapshots.AddRange(snapshots);

            await _context.SaveChangesAsync();
        }
    }
}
