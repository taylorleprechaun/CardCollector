using CardCollector.Data.Models;

namespace CardCollector.Repository
{
    /// <summary>
    /// Stores and retrieves per-card market-value snapshots taken during each valuation run.
    /// </summary>
    public interface ICollectionEntryValueRepository
    {
        /// <summary>
        /// Returns the most recent set of per-entry value snapshots.
        /// </summary>
        Task<IEnumerable<CollectionEntryValueSnapshot>> GetLatestSnapshotsAsync();

        /// <summary>
        /// Inserts or updates entry-level value snapshots for the given snapshot date.
        /// </summary>
        Task UpsertSnapshotsAsync(IEnumerable<CollectionEntryValueSnapshot> snapshots, string snapshotDate);
    }
}
