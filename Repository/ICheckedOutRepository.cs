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
        /// Returns the checked-out record for the given (imageID, setCode, rarityName) combination, or null if not found.
        /// </summary>
        Task<CheckedOutCard?> GetAsync(int imageID, string setCode, string rarityName);

        /// <summary>
        /// Returns a lookup mapping (imageID, setCode, rarityName) combinations to their checked-out date and quantity.
        /// </summary>
        Task<IReadOnlyDictionary<(int ImageID, string SetCode, string RarityName), (DateTime Date, int Quantity)>> GetCheckedOutLookupAsync();

        /// <summary>
        /// Deletes the checked-out record for the given (imageID, setCode, rarityName) combination. Returns false if not found.
        /// </summary>
        Task<bool> RemoveAsync(int imageID, string setCode, string rarityName);

        /// <summary>
        /// Updates the quantity on an existing checked-out record.
        /// </summary>
        Task UpdateAsync(int imageID, string setCode, string rarityName, int quantity);
    }
}
