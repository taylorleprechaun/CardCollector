using CardCollector.Data.Models.Catalog;

namespace CardCollector.Models.Catalog
{
    /// <summary>The outcome of merging one run's detected items into the stored review items.</summary>
    public sealed class CatalogReviewReconcileResult
    {
        /// <summary>Actionable items created or reopened this run; the guardrail compares this to its limit.</summary>
        public int ActionableOpened { get; init; }

        public int AutoResolved { get; init; }

        /// <summary>Items that did not exist before; they need adding to the database.</summary>
        public IReadOnlyList<CatalogReviewItem> NewItems { get; init; } = [];

        /// <summary>Items of each kind created or reopened this run.</summary>
        public IReadOnlyDictionary<CatalogReviewItemKind, int> OpenedByKind { get; init; } = new Dictionary<CatalogReviewItemKind, int>();
    }
}
