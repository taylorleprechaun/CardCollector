using CardCollector.Data.Models;

namespace CardCollector.Repository
{
    /// <summary>
    /// Provides data access for checked-out card records, one per catalog printing.
    /// </summary>
    public interface ICheckedOutRepository
    {
        /// <summary>
        /// Persists a new checked-out card record.
        /// </summary>
        Task AddAsync(CheckedOutCard entry);

        /// <summary>
        /// Returns all checked-out card records ordered by checkout date descending.
        /// </summary>
        Task<IReadOnlyList<CheckedOutCard>> GetAllAsync();

        /// <summary>
        /// Returns the checked-out record for the catalog printing, or null if it isn't checked out.
        /// </summary>
        Task<CheckedOutCard?> GetAsync(int printingID);

        /// <summary>
        /// Returns each checked-out catalog printing's checkout date and quantity, keyed by printing ID.
        /// </summary>
        Task<IReadOnlyDictionary<int, (DateTime Date, int Quantity)>> GetCheckedOutLookupAsync();

        /// <summary>
        /// Deletes the checked-out record for the catalog printing. Returns false if it wasn't checked out.
        /// </summary>
        Task<bool> RemoveAsync(int printingID);

        /// <summary>
        /// Updates the quantity on the catalog printing's checked-out record, if there is one.
        /// </summary>
        Task UpdateAsync(int printingID, int quantity);
    }
}
