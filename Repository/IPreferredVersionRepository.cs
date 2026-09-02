using CardCollector.Data.Models;

namespace CardCollector.Repository
{
    /// <summary>
    /// Provides data access for the user's tracked-printing selections stored in SQLite. A card can have
    /// any number of tracked printings at once, each identified by (CardID, SetCode, RarityName).
    /// </summary>
    public interface IPreferredVersionRepository
    {
        /// <summary>
        /// Inserts a new tracked printing for the given (cardID, setCode, rarityName) combination, or
        /// updates the existing one if that exact printing is already tracked. When
        /// <paramref name="desiredQuantity"/> is null, a newly-created record defaults to 3 and an
        /// existing one keeps its current target.
        /// </summary>
        Task AddOrUpdateAsync(int cardID, int imageID, string setCode, string? rarityName = null, int? desiredQuantity = null);

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
        /// Returns the tracked printings for the given set of image IDs, grouped by image ID. More than
        /// one tracked printing can share an image ID (different set/rarity printings of the same artwork).
        /// </summary>
        Task<IReadOnlyDictionary<int, IReadOnlyList<PreferredVersion>>> GetByImageIDsAsync(IEnumerable<int> imageIDs);

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
        /// Updates the set/rarity of the tracked printing with the given ID (e.g. swapping to a newer
        /// reprint), preserving its desired quantity. Returns false if no such record exists.
        /// </summary>
        Task<bool> UpgradeAsync(int id, string newSetCode, string newRarityName);
    }
}
