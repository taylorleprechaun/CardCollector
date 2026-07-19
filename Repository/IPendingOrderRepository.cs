using CardCollector.Data.Models;

namespace CardCollector.Repository
{
    /// <summary>
    /// Provides data access for staged (not-yet-committed) purchase-order lines.
    /// </summary>
    public interface IPendingOrderRepository
    {
        /// <summary>
        /// Persists a batch of new pending order lines.
        /// </summary>
        Task AddRangeAsync(IEnumerable<PendingOrderLine> lines);

        /// <summary>
        /// Deletes every pending order line.
        /// </summary>
        Task DeleteAllAsync();

        /// <summary>
        /// Deletes the pending order line with the given ID. Returns false if no such line exists.
        /// </summary>
        Task<bool> DeleteAsync(int id);

        /// <summary>
        /// Returns every pending order line, ordered by date created descending.
        /// </summary>
        Task<IReadOnlyList<PendingOrderLine>> GetAllAsync();

        /// <summary>
        /// Returns the set of (imageID, setCode) pairs present across all staged pending order lines.
        /// </summary>
        Task<IReadOnlySet<(int ImageID, string SetCode)>> GetStagedPairsAsync();

        /// <summary>
        /// Returns the total line count and total cost (price * quantity) across all pending order lines.
        /// </summary>
        Task<(int Count, decimal Total)> GetSummaryAsync();
    }
}
