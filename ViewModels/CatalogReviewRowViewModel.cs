namespace CardCollector.ViewModels
{
    /// <summary>The model of one inbox row partial: the item plus the query string that keeps the current filter on post.</summary>
    public sealed class CatalogReviewRowViewModel
    {
        public string FilterParams { get; init; } = string.Empty;

        public CatalogReviewItemViewModel Item { get; init; } = new();
    }
}
