namespace CardCollector.ViewModels
{
    /// <summary>The model of one inbox row partial: the item plus the query string that keeps the current filter on post.</summary>
    public sealed class CatalogReviewRowViewModel
    {
        public string FilterParams { get; init; } = string.Empty;

        public CatalogReviewItemViewModel Item { get; init; } = new();

        /// <summary>True when the list mixes kinds (no Kind filter), so each row names its kind.</summary>
        public bool ShowKind { get; init; }
    }
}
