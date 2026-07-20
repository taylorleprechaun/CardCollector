using CardCollector.Data.Models;

namespace CardCollector.Repository
{
    /// <summary>
    /// Provides data access for the user's card collection stored in SQLite.
    /// </summary>
    public interface ICollectionRepository
    {
        /// <summary>
        /// Persists a new collection entry.
        /// </summary>
        Task AddAsync(CollectionEntry entry);

        /// <summary>
        /// Deletes the entry with the given ID. Returns false if no such entry exists.
        /// </summary>
        Task<bool> DeleteAsync(int id);

        /// <summary>
        /// Returns all entries for the given card ID across all artworks.
        /// </summary>
        Task<IEnumerable<CollectionEntry>> GetByCardIDAsync(int cardID);

        /// <summary>
        /// Returns the entry with the given ID, or null if no such entry exists.
        /// </summary>
        Task<CollectionEntry?> GetByIDAsync(int id);

        /// <summary>
        /// Returns all entries with the given collection status, ordered by date created descending.
        /// </summary>
        Task<IEnumerable<CollectionEntry>> GetByStatusAsync(CollectionStatus status);

        /// <summary>
        /// Returns the set of (imageID, setCode) pairs present in the collection regardless of status.
        /// </summary>
        Task<IReadOnlySet<(int ImageID, string SetCode)>> GetCollectedPairsAsync();

        /// <summary>
        /// Returns the total Ordered quantity for every (imageID, setCode, rarityName) combination, summed
        /// across matching entries. RarityName is normalized to an empty string when null, so callers should
        /// look up with <c>rarityName ?? string.Empty</c>.
        /// </summary>
        Task<IReadOnlyDictionary<(int ImageID, string SetCode, string RarityName), int>> GetOrderedQuantitiesAsync();

        /// <summary>
        /// Returns the set of (imageID, setCode) pairs present in Owned entries only.
        /// </summary>
        Task<IReadOnlySet<(int ImageID, string SetCode)>> GetOwnedPairsAsync();

        /// <summary>
        /// Returns the completion status (Complete, Incomplete, or Placeholder) for each owned image ID in the given set.
        /// Image IDs that are not owned are omitted from the result.
        /// </summary>
        Task<IReadOnlyDictionary<int, CollectionCompletionStatus>> GetCompletionStatusByImageIDsAsync(IEnumerable<int> imageIDs);

        /// <summary>
        /// Returns the distinct non-null acquisition methods present in owned entries, sorted.
        /// </summary>
        Task<IReadOnlyList<AcquisitionMethod>> GetDistinctAcquisitionMethodsAsync();

        /// <summary>
        /// Returns the distinct non-null conditions present in owned entries, sorted.
        /// </summary>
        Task<IReadOnlyList<CardCondition>> GetDistinctConditionsAsync();

        /// <summary>
        /// Returns the distinct non-null editions present in owned entries, sorted.
        /// </summary>
        Task<IReadOnlyList<CardEdition>> GetDistinctEditionsAsync();

        /// <summary>
        /// Returns the distinct non-null rarity names present in owned entries, sorted.
        /// </summary>
        Task<IReadOnlyList<string>> GetDistinctRarityNamesAsync();

        /// <summary>
        /// Returns the distinct set codes present in owned entries, sorted.
        /// </summary>
        Task<IReadOnlyList<string>> GetDistinctSetCodesAsync();

        /// <summary>
        /// Returns the total owned quantity per card ID, scoped to entries whose set code starts with the given
        /// prefix (e.g. "MP25" matches "MP25-EN001"). Card IDs with no matching owned entries are omitted from the result.
        /// </summary>
        Task<IReadOnlyDictionary<int, int>> GetOwnedQuantitiesByCardIDsForSetPrefixAsync(IEnumerable<int> cardIDs, string setPrefix);

        /// <summary>
        /// Returns the total owned quantity per (imageID, setCode, rarityName) combination for the given set of combinations.
        /// Combinations with no owned entries are omitted from the result.
        /// </summary>
        Task<IReadOnlyDictionary<(int ImageID, string SetCode, string RarityName), int>> GetOwnedQuantitiesForPairsAsync(IEnumerable<(int ImageID, string SetCode, string RarityName)> pairs);

        /// <summary>
        /// Returns the rarity-aware owned quantity per (imageID, setCode) pair for the given preferred versions.
        /// When a preferred version specifies a non-null rarityName, only owned entries with a matching rarity are counted.
        /// Pairs with no matching owned entries are omitted from the result.
        /// </summary>
        Task<IReadOnlyDictionary<(int ImageID, string SetCode), int>> GetOwnedQuantitiesForPreferredVersionsAsync(IEnumerable<(int ImageID, string SetCode, string? RarityName)> preferredVersions);

        /// <summary>
        /// Returns quantity, market-value-at-entry, and purchase-price totals for owned entries.
        /// </summary>
        Task<OwnedCollectionStats> GetOwnedStatsAsync();

        /// <summary>
        /// Returns distinct card IDs that have at least one entry with the given status.
        /// </summary>
        Task<IReadOnlySet<int>> GetCardIDsByStatusAsync(CollectionStatus status);

        /// <summary>
        /// Returns the highest-priority status (Owned beats Ordered) per card ID for the given set of card IDs.
        /// </summary>
        Task<IReadOnlyDictionary<int, CollectionStatus>> GetStatusByCardIDsAsync(IEnumerable<int> cardIDs);

        /// <summary>
        /// Updates the mutable fields of an existing entry. Returns false if no such entry exists.
        /// </summary>
        Task<bool> UpdateAsync(CollectionEntry entry);

        /// <summary>
        /// Updates the status (and optionally quantity) of an existing entry. Returns false if no such entry exists.
        /// </summary>
        Task<bool> UpdateStatusAsync(int id, CollectionStatus status, int? quantity = null);
    }
}
