using CardCollector.Data.Models;

namespace CardCollector.Repository
{
    /// <summary>
    /// Provides data access for the user's preferred version selections stored in SQLite.
    /// </summary>
    public interface IPreferredVersionRepository
    {
        /// <summary>
        /// Inserts or updates the preferred printing for the given (cardID, imageID, setCode) combination.
        /// </summary>
        Task AddOrUpdateAsync(int cardID, int imageID, string setCode, string? rarityName = null);

        /// <summary>
        /// Deletes the preferred version for the given image ID.
        /// </summary>
        Task DeleteAsync(int imageID);

        /// <summary>
        /// Returns all preferred version records.
        /// </summary>
        Task<IEnumerable<PreferredVersion>> GetAllAsync();

        /// <summary>
        /// Returns a dictionary of preferred versions keyed by image ID for the given set of image IDs.
        /// </summary>
        Task<Dictionary<int, PreferredVersion>> GetByImageIDsAsync(IEnumerable<int> imageIDs);

        /// <summary>
        /// Returns the set of image IDs that have a preferred version saved.
        /// </summary>
        Task<HashSet<int>> GetPreferredImageIDsAsync();
    }
}
