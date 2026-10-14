using CardCollector.Data.Models.Catalog;

namespace CardCollector.Models.Catalog
{
    /// <summary>
    /// The outcome of one catalog sync run, as saved in its <see cref="CatalogSyncRun"/> row, or of a start-up sync that was
    /// skipped because nothing changed.
    /// </summary>
    public sealed class CatalogSyncResult
    {
        /// <summary>New and reopened items of each kind in this run.</summary>
        public IReadOnlyDictionary<CatalogReviewItemKind, int> ItemsOpenedByKind { get; init; } = new Dictionary<CatalogReviewItemKind, int>();

        /// <summary>The run this sync recorded; for a skipped sync, the earlier run that already processed the same data.</summary>
        public CatalogSyncRun Run { get; init; } = new();

        /// <summary>True when the sources hadn't changed since the last successful run, so no run was recorded.</summary>
        public bool Skipped { get; init; }
    }
}
