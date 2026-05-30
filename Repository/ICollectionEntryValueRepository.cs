using CardCollector.Data.Models;

namespace CardCollector.Repository
{
    /// <summary>
    /// Stores and retrieves per-card market-value snapshots taken during each valuation run.
    /// </summary>
    public interface ICollectionEntryValueRepository
    {
        Task<IEnumerable<CollectionEntryValueSnapshot>> GetLatestSnapshotsAsync();
        Task UpsertSnapshotsAsync(IEnumerable<CollectionEntryValueSnapshot> snapshots, string snapshotDate);
    }
}
