using CardCollector.Data.Models.Catalog;

namespace CardCollector.ViewModels
{
    public sealed class CatalogReviewSearchCriteria
    {
        public CatalogReviewFilter Filter { get; init; } = CatalogReviewFilter.NeedsDecision;

        /// <summary>Only items of this kind; null for every kind the filter allows.</summary>
        public CatalogReviewItemKind? Kind { get; init; }

        public int Page { get; init; } = 1;

        public int PageSize { get; init; } = Paging.DEFAULT_PAGE_SIZE;

        /// <summary>Text the summary, resolution, or card name must contain; null or blank for no search.</summary>
        public string? Query { get; init; }
    }
}
