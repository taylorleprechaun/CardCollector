using CardCollector.Data.Models;

namespace CardCollector.Repository
{
    /// <summary>
    /// Stores and retrieves per-card market-value snapshots taken during each valuation run.
    /// </summary>
    public interface ICollectionEntryValueRepository
    {
        /// <summary>
        /// Returns the distinct card names that have at least one value snapshot, sorted alphabetically.
        /// </summary>
        Task<IEnumerable<string>> GetDistinctCardNamesAsync();

        /// <summary>
        /// Returns all value snapshots for the given card name across all snapshot dates, ordered by date ascending.
        /// </summary>
        Task<IEnumerable<CollectionEntryValueSnapshot>> GetHistoryByCardNameAsync(string cardName);

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
