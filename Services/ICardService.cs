using CardCollector.DTO;
using CardCollector.ViewModels;

namespace CardCollector.Services
{
    /// <summary>
    /// Coordinates card data from the JSON source with the user's collection state in SQLite.
    /// </summary>
    public interface ICardService
    {
        Task<DashboardStats> GetDashboardStatsAsync();
        Task<IEnumerable<OrderEntryViewModel>> GetEnrichedOrdersAsync();
        Task<IEnumerable<OrderEntryViewModel>> GetEnrichedOwnedAsync();
        Task<IEnumerable<CollectionGroupViewModel>> GetGroupedOwnedAsync();
        Card? GetCardByID(int cardID);
        Task<(Card Card, Image Image)?> GetRandomUncollectedAsync();
        Task<PagedResult<CardListItemViewModel>> SearchCardsAsync(string? query, int page, int pageSize);
    }
}
