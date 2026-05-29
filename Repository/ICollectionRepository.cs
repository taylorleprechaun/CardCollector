using CardCollector.Data.Models;

namespace CardCollector.Repository
{
    /// <summary>
    /// Provides data access for the user's card collection stored in SQLite.
    /// </summary>
    public interface ICollectionRepository
    {
        Task AddAsync(CollectionEntry entry);
        Task<bool> DeleteAsync(int id);
        Task<bool> ExistsAsync(int imageID, string setCode);
        Task<IEnumerable<CollectionEntry>> GetByStatusAsync(CollectionStatus status);
        Task<IEnumerable<int>> GetCollectedImageIDsAsync();
        Task<HashSet<(int ImageID, string SetCode)>> GetCollectedPairsAsync();
        Task<HashSet<int>> GetPlaceholderCardIDsAsync(IEnumerable<int> cardIDs);
        Task<Dictionary<int, CollectionStatus>> GetStatusByCardIDsAsync(IEnumerable<int> cardIDs);
        Task<bool> UpdateAsync(CollectionEntry entry);
        Task<bool> UpdateStatusAsync(int id, CollectionStatus status, int? quantity = null);
    }
}
