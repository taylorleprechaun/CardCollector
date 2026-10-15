using CardCollector.Data.Models;

namespace CardCollector.Repository
{
    /// <summary>
    /// Stores and retrieves per-card market-value snapshots taken during each valuation run.
    /// </summary>
    public interface ICollectionEntryValueRepository
    {
        /// <summary>
        /// Returns every catalog card that has at least one value snapshot.
        /// </summary>
        Task<IReadOnlySet<int>> GetCatalogCardIDsAsync();

        /// <summary>
        /// Returns all value snapshots for the given catalog card across all snapshot dates, ordered by date ascending.
        /// </summary>
        Task<IEnumerable<CollectionEntryValueSnapshot>> GetHistoryByCardIDAsync(int cardID);

        /// <summary>
        /// Returns the most recent snapshot for each collection entry, regardless of whether all entries share the same snapshot date.
        /// </summary>
        Task<IEnumerable<CollectionEntryValueSnapshot>> GetLatestSnapshotsAsync();

        /// <summary>
        /// Deletes per-card snapshots older than 30 days, keeping only the rows from the most recent
        /// snapshot date per calendar month for older data.
        /// </summary>
        Task PruneSnapshotsAsync();
        
        /// <summary>
        /// Inserts or updates entry-level value snapshots for the given snapshot date.
        /// </summary>
        Task UpsertSnapshotsAsync(IEnumerable<CollectionEntryValueSnapshot> snapshots, string snapshotDate);
    }
}
