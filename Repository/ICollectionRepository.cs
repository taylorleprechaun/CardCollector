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
        /// Returns all entries for the given image ID.
        /// </summary>
        Task<IEnumerable<CollectionEntry>> GetByImageIDAsync(int imageID);

        /// <summary>
        /// Returns all entries with the given collection status, ordered by date created descending.
        /// </summary>
        Task<IEnumerable<CollectionEntry>> GetByStatusAsync(CollectionStatus status);

        /// <summary>
        /// Returns the set of (imageID, setCode) pairs present in the collection regardless of status.
        /// </summary>
        Task<IReadOnlySet<(int ImageID, string SetCode)>> GetCollectedPairsAsync();

        /// <summary>
        /// Returns the completion status (Complete, Incomplete, or Placeholder) for each owned image ID in the given set.
        /// Image IDs that are not owned are omitted from the result.
        /// </summary>
        Task<IReadOnlyDictionary<int, CollectionCompletionStatus>> GetCompletionStatusByImageIDsAsync(IEnumerable<int> imageIDs);

        /// <summary>
        /// Returns quantity, market-value-at-entry, and purchase-price totals for owned entries.
        /// </summary>
        Task<OwnedCollectionStats> GetOwnedStatsAsync();

        /// <summary>
        /// Returns the card IDs from the given set that are owned but do not match any preferred version.
        /// </summary>
        Task<IReadOnlySet<int>> GetPlaceholderCardIDsAsync(IEnumerable<int> cardIDs);

        /// <summary>
        /// Returns the image IDs from the given set that are owned but do not match any preferred version.
        /// </summary>
        Task<IReadOnlySet<int>> GetPlaceholderImageIDsAsync(IEnumerable<int> imageIDs);

        /// <summary>
        /// Returns the highest-priority status (Owned beats Ordered) per card ID for the given set of card IDs.
        /// </summary>
        Task<IReadOnlyDictionary<int, CollectionStatus>> GetStatusByCardIDsAsync(IEnumerable<int> cardIDs);

        /// <summary>
        /// Returns the highest-priority status (Owned beats Ordered) per image ID for the given set of image IDs.
        /// </summary>
        Task<IReadOnlyDictionary<int, CollectionStatus>> GetStatusByImageIDsAsync(IEnumerable<int> imageIDs);

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
