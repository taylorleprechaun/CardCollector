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
        /// <summary>
        /// Adds a new entry to the collection. Returns false if an entry for the same (imageID, setCode) already exists.
        /// </summary>
        Task<bool> AddEntryAsync(
            int cardID, int imageID, string setCode, CollectionStatus status,
            int quantity, CardCondition? condition, CardEdition? edition,
            AcquisitionMethod? acquisitionMethod, bool isPlaceholder,
            DateTime? purchaseDate, decimal? purchasePrice, decimal? marketPriceAtEntry = null,
            string? rarityName = null);

        /// <summary>
        /// Fetches live prices for all owned entries, persists a daily snapshot, and returns the total value with a per-set breakdown.
        /// </summary>
        Task<(decimal TotalValue, int CardCount, List<(string Label, decimal Value)> SetValueBreakdown)> CalculateCurrentMarketValueAsync();

        /// <summary>
        /// Returns the card with the given ID from the in-memory card data, or null if not found.
        /// </summary>
        Card? GetCardByID(int cardID);

        /// <summary>
        /// Returns up to maxResults card name suggestions whose names contain the given query string.
        /// </summary>
        IEnumerable<string> GetCardNameSuggestions(string query, int maxResults = 10);

        /// <summary>
        /// Returns aggregated collection statistics including rarity, set, acquisition, and value breakdowns.
        /// </summary>
        Task<CollectionStatsViewModel> GetCollectionStatsAsync();

        /// <summary>
        /// Returns high-level stats for the dashboard: counts, market value, and wishlist size.
        /// </summary>
        Task<DashboardStats> GetDashboardStatsAsync();

        /// <summary>
        /// Returns all ordered entries enriched with card and set data.
        /// </summary>
        Task<IEnumerable<OrderEntryViewModel>> GetEnrichedOrdersAsync();

        /// <summary>
        /// Returns all owned entries enriched with card and set data.
        /// </summary>
        Task<IEnumerable<OrderEntryViewModel>> GetEnrichedOwnedAsync();

        /// <summary>
        /// Returns owned entries grouped by printing, annotated with preferred-version and completion status.
        /// </summary>
        Task<IEnumerable<CollectionGroupViewModel>> GetGroupedOwnedAsync();

        /// <summary>
        /// Returns the preferred version for the given image ID, or null if none is set.
        /// </summary>
        Task<PreferredVersion?> GetPreferredVersionByImageIDAsync(int imageID);

        /// <summary>
        /// Returns a random artwork that has not yet been added to the user's preferred versions.
        /// </summary>
        Task<(Card Card, Image Image)?> GetRandomUncollectedAsync();

        /// <summary>
        /// Returns all preferred versions that have not yet been ordered or owned.
        /// </summary>
        Task<IEnumerable<WishlistItemViewModel>> GetWishlistAsync();

        /// <summary>
        /// Deletes the preferred version for the given image ID, removing the card from the wishlist.
        /// </summary>
        Task RemoveFromWishlistAsync(int imageID);

        /// <summary>
        /// Saves or updates the preferred printing for the given (cardID, imageID, setCode) combination.
        /// </summary>
        Task SavePreferredVersionAsync(int cardID, int imageID, string setCode, string? rarityName = null);

        /// <summary>
        /// Returns a paginated, filtered page of browseable card artworks matching the given criteria.
        /// </summary>
        Task<PagedResult<CardListItemViewModel>> SearchCardsAsync(BrowseSearchCriteria criteria);

        /// <summary>
        /// Returns a paginated, filtered page of owned collection groups matching the given query.
        /// </summary>
        Task<PagedResult<CollectionGroupViewModel>> SearchGroupedOwnedAsync(string? query, int page, int pageSize);

        /// <summary>
        /// Returns a paginated, filtered, and sorted page of wishlist items.
        /// </summary>
        Task<WishlistSearchResult> SearchWishlistAsync(string? query, int page, int pageSize, WishlistSortBy sortBy = WishlistSortBy.Name, bool sortDescending = false);
    }
}
