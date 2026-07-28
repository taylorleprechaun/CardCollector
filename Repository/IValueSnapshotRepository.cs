using CardCollector.Data.Models;

namespace CardCollector.Repository
{
    /// <summary>
    /// Stores and retrieves day-level aggregate value snapshots for a single snapshot table.
    /// </summary>
    public interface IValueSnapshotRepository<TEntity> where TEntity : class, IValueSnapshotEntity
    {
        /// <summary>
        /// Returns all historical snapshots in ascending date order.
        /// </summary>
        Task<IEnumerable<TEntity>> GetAllSnapshotsAsync();

        /// <summary>
        /// Returns the most recent snapshot, or null if none exists.
        /// </summary>
        Task<TEntity?> GetLatestSnapshotAsync();

        /// <summary>
        /// Deletes snapshots older than 30 days, keeping only the most recent
        /// snapshot per calendar month for data beyond the 30-day window.
        /// </summary>
        Task PruneSnapshotsAsync();

        /// <summary>
        /// Inserts or updates the snapshot for its date.
        /// </summary>
        Task UpsertSnapshotAsync(TEntity snapshot);
    }
}
