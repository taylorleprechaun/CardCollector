using CardCollector.Data.Models.Catalog;

namespace CardCollector.Models.Catalog
{
    /// <summary>The outcome of one catalog sync run, as saved in its <see cref="CatalogSyncRun"/> row.</summary>
    public sealed class CatalogSyncResult
    {
        /// <summary>New and reopened items of each kind in this run.</summary>
        public IReadOnlyDictionary<CatalogReviewItemKind, int> ItemsOpenedByKind { get; init; } = new Dictionary<CatalogReviewItemKind, int>();

        public CatalogSyncRun Run { get; init; } = new();
    }
}
