using CardCollector.Data.Models;

namespace CardCollector.Repository
{
    /// <summary>
    /// Stores and retrieves daily market-value snapshots for the collection.
    /// </summary>
    public interface ICollectionValueRepository
    {
        Task<IEnumerable<CollectionValueSnapshot>> GetAllSnapshotsAsync();
        Task<CollectionValueSnapshot?> GetLatestSnapshotAsync();
        Task UpsertSnapshotAsync(CollectionValueSnapshot snapshot);
    }
}
