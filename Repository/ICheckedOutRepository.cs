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
        /// Returns the checked-out record for the given (cardID, setCode, rarityName, printVariant) combination, or null if not found.
        /// </summary>
        Task<CheckedOutCard?> GetAsync(int cardID, string setCode, string rarityName, string? printVariant = null);

        /// <summary>
        /// Returns a lookup mapping (cardID, setCode, rarityName, printVariant) combinations to their checked-out date and quantity.
        /// </summary>
        Task<IReadOnlyDictionary<(int CardID, string SetCode, string RarityName, string? PrintVariant), (DateTime Date, int Quantity)>> GetCheckedOutLookupAsync();

        /// <summary>
        /// Deletes the checked-out record for the given (cardID, setCode, rarityName, printVariant) combination. Returns false if not found.
        /// </summary>
        Task<bool> RemoveAsync(int cardID, string setCode, string rarityName, string? printVariant = null);

        /// <summary>
        /// Updates the quantity on an existing checked-out record.
        /// </summary>
        Task UpdateAsync(int cardID, string setCode, string rarityName, int quantity, string? printVariant = null);
    }
}
