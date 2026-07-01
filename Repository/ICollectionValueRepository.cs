using CardCollector.Data.Models;

namespace CardCollector.Repository
{
    /// <summary>
    /// Stores and retrieves daily market-value snapshots for the collection.
    /// </summary>
    public interface ICollectionValueRepository
    {
        /// <summary>
        /// Returns all historical daily collection-value snapshots in ascending date order.
        /// </summary>
        Task<IEnumerable<CollectionValueSnapshot>> GetAllSnapshotsAsync();

        /// <summary>
        /// Returns the most recent daily collection-value snapshot, or null if none exists.
        /// </summary>
        Task<CollectionValueSnapshot?> GetLatestSnapshotAsync();

        /// <summary>
        /// Deletes daily snapshots older than 30 days, keeping only the most recent
        /// snapshot per calendar month for data beyond the 30-day window.
        /// </summary>
        Task PruneSnapshotsAsync();
        
        /// <summary>
        /// Inserts or updates the daily collection-value snapshot.
        /// </summary>
        Task UpsertSnapshotAsync(CollectionValueSnapshot snapshot);
    }
}
