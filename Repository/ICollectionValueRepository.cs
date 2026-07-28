using CardCollector.Data.Models;

namespace CardCollector.Repository
{
    /// <summary>
    /// Stores and retrieves daily market-value snapshots for the collection.
    /// </summary>
    public interface ICollectionValueRepository : IValueSnapshotRepository<CollectionValueSnapshot>
    {
    }
}
