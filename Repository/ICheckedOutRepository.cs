using CardCollector.Data.Models;

namespace CardCollector.Repository
{
    /// <summary>
    /// Provides data access for checked-out card records.
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
        /// Returns the checked-out record for the given (imageID, setCode) pair, or null if not found.
        /// </summary>
        Task<CheckedOutCard?> GetAsync(int imageID, string setCode);

        /// <summary>
        /// Returns a lookup mapping (imageID, setCode) pairs to their checked-out date and quantity.
        /// </summary>
        Task<IReadOnlyDictionary<(int ImageID, string SetCode), (DateTime Date, int Quantity)>> GetCheckedOutLookupAsync();

        /// <summary>
        /// Deletes the checked-out record for the given (imageID, setCode) pair. Returns false if not found.
        /// </summary>
        Task<bool> RemoveAsync(int imageID, string setCode);

        /// <summary>
        /// Updates the quantity on an existing checked-out record.
        /// </summary>
        Task UpdateAsync(int imageID, string setCode, int quantity);
    }
}
