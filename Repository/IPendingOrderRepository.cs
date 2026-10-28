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
        /// Deletes the pending order line with the given ID. Returns false if no such line exists.
        /// </summary>
        Task<bool> DeleteAsync(int id);

        /// <summary>
        /// Deletes the pending order lines with the given IDs. IDs that don't match an existing line are ignored.
        /// </summary>
        Task DeleteRangeAsync(IEnumerable<int> ids);

        /// <summary>
        /// Returns every pending order line, ordered by date created descending.
        /// </summary>
        Task<IReadOnlyList<PendingOrderLine>> GetAllAsync();

        /// <summary>
        /// Returns the pending order lines with the given IDs. IDs that don't match an existing line are omitted.
        /// </summary>
        Task<IReadOnlyList<PendingOrderLine>> GetByIDsAsync(IEnumerable<int> ids);

        /// <summary>
        /// Returns the total quantity staged in the cart for each catalog printing.
        /// </summary>
        Task<IReadOnlyDictionary<int, int>> GetStagedQuantitiesAsync();

        /// <summary>
        /// Returns the total line count and total cost (price * quantity) across all pending order lines.
        /// </summary>
        Task<(int Count, decimal Total)> GetSummaryAsync();

        /// <summary>
        /// Updates the editable fields (condition, edition, market price, purchase date, purchase price, and quantity)
        /// of the pending order line whose ID matches <paramref name="line"/>. Returns false if no such line exists.
        /// </summary>
        Task<bool> UpdateAsync(PendingOrderLine line);
    }
}
