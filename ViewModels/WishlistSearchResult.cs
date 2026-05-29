namespace CardCollector.ViewModels
{
    public sealed class WishlistSearchResult
    {
        public PagedResult<WishlistItemViewModel> PagedItems { get; init; } = new();
        public decimal WishlistTotal { get; init; }
    }
}
