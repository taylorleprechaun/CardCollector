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
        /// Returns all entries for the given card ID.
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
        /// Returns distinct card IDs that have at least one entry with the given status.
        /// </summary>
        Task<IReadOnlySet<int>> GetCardIDsByStatusAsync(CollectionStatus status);

        /// <summary>
        /// Returns the completion status (Complete, Incomplete, or Placeholder) for each owned card ID in the given set.
        /// Card IDs that are not owned are omitted from the result.
        /// </summary>
        Task<IReadOnlyDictionary<int, CollectionCompletionStatus>> GetCompletionStatusByCardIDsAsync(IEnumerable<int> cardIDs);

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
        /// Returns the total Ordered quantity of each catalog printing.
        /// </summary>
        Task<IReadOnlyDictionary<int, int>> GetOrderedQuantitiesAsync();

        /// <summary>
        /// Returns each distinct (card, catalog printing) that has an Owned entry, for cross-referencing against tracked
        /// printings.
        /// </summary>
        Task<IReadOnlyList<(int CardID, int PrintingID)>> GetOwnedCardPrintingsAsync();

        /// <summary>
        /// Returns the catalog printing IDs that have at least one Owned entry.
        /// </summary>
        Task<IReadOnlySet<int>> GetOwnedPrintingIDsAsync();

        /// <summary>
        /// Returns the total owned quantity of each of the given catalog printings. Printings with no owned entries are
        /// omitted from the result.
        /// </summary>
        Task<IReadOnlyDictionary<int, int>> GetOwnedQuantitiesByPrintingAsync(IEnumerable<int> printingIDs);

        /// <summary>
        /// Returns quantity, market-value-at-entry, and purchase-price totals for owned entries.
        /// </summary>
        Task<OwnedCollectionStats> GetOwnedStatsAsync();

        /// <summary>
        /// Returns the total quantity of each catalog printing among the given cards' entries with the given status.
        /// Printings with no such entries are omitted from the result.
        /// </summary>
        Task<IReadOnlyDictionary<int, int>> GetQuantitiesByPrintingAsync(IEnumerable<int> cardIDs, CollectionStatus status);

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
