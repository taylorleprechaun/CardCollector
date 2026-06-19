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
        /// Returns the most recent set of per-entry value snapshots.
        /// </summary>
        Task<IEnumerable<CollectionEntryValueSnapshot>> GetLatestSnapshotsAsync();

        /// <summary>
        /// Inserts or updates entry-level value snapshots for the given snapshot date.
        /// </summary>
        Task UpsertSnapshotsAsync(IEnumerable<CollectionEntryValueSnapshot> snapshots, string snapshotDate);
    }
}
