namespace CardCollector.ViewModels
{
    public sealed class WishlistSearchCriteria : SearchCriteria
    {
        public WishlistSortBy SortBy { get; set; } = WishlistSortBy.Name;
        public bool SortDescending { get; set; }
    }
}
