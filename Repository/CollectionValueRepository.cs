using CardCollector.Data;
using CardCollector.Data.Models;

namespace CardCollector.Repository
{
    public sealed class CollectionValueRepository : ValueSnapshotRepository<CollectionValueSnapshot>, ICollectionValueRepository
    {
        public CollectionValueRepository(AppDBContext context) : base(context, "CollectionValueSnapshots")
        {
        }
    }
}
