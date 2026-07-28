using CardCollector.Data;
using CardCollector.Data.Models;

namespace CardCollector.Repository
{
    public sealed class WishlistValueRepository : ValueSnapshotRepository<WishlistValueSnapshot>, IWishlistValueRepository
    {
        public WishlistValueRepository(AppDBContext context) : base(context, "WishlistValueSnapshots")
        {
        }
    }
}
