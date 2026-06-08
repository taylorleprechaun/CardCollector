namespace CardCollector.ViewModels
{
    public sealed class WishlistSearchCriteria
    {
        public string? CardType { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
        public string? Query { get; set; }
        public string? RarityName { get; set; }
        public string? SetName { get; set; }
        public WishlistSortBy SortBy { get; set; } = WishlistSortBy.Name;
        public bool SortDescending { get; set; }
    }
}
