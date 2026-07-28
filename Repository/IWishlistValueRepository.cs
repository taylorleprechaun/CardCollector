using CardCollector.Data.Models;

namespace CardCollector.Repository
{
    /// <summary>
    /// Stores and retrieves daily wishlist cost-to-complete snapshots.
    /// </summary>
    public interface IWishlistValueRepository : IValueSnapshotRepository<WishlistValueSnapshot>
    {
    }
}
