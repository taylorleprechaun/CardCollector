using CardCollector.Data.Models;

namespace CardCollector.Repository
{
    /// <summary>
    /// Provides data access for the user's tracked-printing selections stored in SQLite. A card can have
    /// any number of tracked printings at once, each identified by (CardID, SetCode, RarityName, PrintVariant).
    /// </summary>
    public interface IPreferredVersionRepository
    {
        /// <summary>
        /// Tracks the catalog printing for the card, or updates its desired quantity when it is already tracked. A new
        /// tracked printing wants 3 copies unless <paramref name="desiredQuantity"/> says otherwise.
        /// </summary>
        Task AddOrUpdateAsync(int printingID, int cardID, int? desiredQuantity = null);

        /// <summary>
        /// Deletes the tracked printing with the given ID.
        /// </summary>
        Task DeleteAsync(int id);

        /// <summary>
        /// Returns all tracked-printing records.
        /// </summary>
        Task<IEnumerable<PreferredVersion>> GetAllAsync();

        /// <summary>
        /// Returns every tracked printing for the given card ID.
        /// </summary>
        Task<IReadOnlyList<PreferredVersion>> GetByCardIDAsync(int cardID);

        /// <summary>
        /// Returns the tracked printings for the given set of card IDs, grouped by card ID. More than
        /// one tracked printing can share a card ID (different set/rarity/variant printings).
        /// </summary>
        Task<IReadOnlyDictionary<int, IReadOnlyList<PreferredVersion>>> GetByCardIDsAsync(IEnumerable<int> cardIDs);

        /// <summary>
        /// Returns the set of card IDs that have at least one tracked printing.
        /// </summary>
        Task<IReadOnlySet<int>> GetPreferredCardIDsAsync();

        /// <summary>
        /// Updates the desired quantity for the tracked printing with the given ID.
        /// Returns false if no such record exists.
        /// </summary>
        Task<bool> UpdateDesiredQuantityAsync(int id, int desiredQuantity);

        /// <summary>
        /// Moves a tracked printing to another catalog printing of the same card, keeping its desired quantity. Returns
        /// false if no such tracked printing exists.
        /// </summary>
        Task<bool> UpgradeAsync(int id, int newPrintingID);
    }
}
