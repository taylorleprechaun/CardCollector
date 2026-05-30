using CardCollector.Data.Models;
using CardCollector.DTO;
using CardCollector.ViewModels;

namespace CardCollector.Services
{
    /// <summary>
    /// Coordinates card data from the JSON source with the user's collection state in SQLite.
    /// </summary>
    public interface ICardService
    {
        Task<bool> AddEntryAsync(
            int cardID, int imageID, string setCode, CollectionStatus status,
            int quantity, CardCondition? condition, CardEdition? edition,
            AcquisitionMethod? acquisitionMethod, bool isPlaceholder,
            DateTime? purchaseDate, decimal? purchasePrice, decimal? marketPriceAtEntry = null,
            string? rarityName = null);
        Card? GetCardByID(int cardID);
        Task<PreferredVersion?> GetPreferredVersionByImageIDAsync(int imageID);
        IEnumerable<string> GetCardNameSuggestions(string query, int maxResults = 10);
        Task<DashboardStats> GetDashboardStatsAsync();
        Task<IEnumerable<OrderEntryViewModel>> GetEnrichedOrdersAsync();
        Task<IEnumerable<OrderEntryViewModel>> GetEnrichedOwnedAsync();
        Task<IEnumerable<CollectionGroupViewModel>> GetGroupedOwnedAsync();
        Task<(Card Card, Image Image)?> GetRandomUncollectedAsync();
        Task RemoveFromWishlistAsync(int imageID);
        Task SavePreferredVersionAsync(int cardID, int imageID, string setCode, string? rarityName = null);
        Task<PagedResult<CardListItemViewModel>> SearchCardsAsync(BrowseSearchCriteria criteria);
        Task<PagedResult<CollectionGroupViewModel>> SearchGroupedOwnedAsync(string? query, int page, int pageSize);
        Task<WishlistSearchResult> SearchWishlistAsync(string? query, int page, int pageSize, WishlistSortBy sortBy = WishlistSortBy.Name, bool sortDescending = false);
        Task<IEnumerable<WishlistItemViewModel>> GetWishlistAsync();
    }
}
