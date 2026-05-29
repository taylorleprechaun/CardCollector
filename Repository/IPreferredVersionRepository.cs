using CardCollector.Data.Models;

namespace CardCollector.Repository
{
    /// <summary>
    /// Provides data access for the user's preferred version selections stored in SQLite.
    /// </summary>
    public interface IPreferredVersionRepository
    {
        Task AddOrUpdateAsync(int cardID, int imageID, string setCode);
        Task DeleteAsync(int imageID);
        Task<IEnumerable<PreferredVersion>> GetAllAsync();
        Task<Dictionary<int, PreferredVersion>> GetByImageIDsAsync(IEnumerable<int> imageIDs);
        Task<HashSet<int>> GetPreferredImageIDsAsync();
    }
}
