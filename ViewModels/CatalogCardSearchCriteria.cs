using CardCollector.Data.Models.Catalog;

namespace CardCollector.ViewModels
{
    /// <summary>Filters for the catalog admin card search.</summary>
    public sealed class CatalogCardSearchCriteria
    {
        public int Page { get; init; } = 1;

        public int PageSize { get; init; } = Paging.DEFAULT_PAGE_SIZE;

        /// <summary>
        /// Part of a card name, the start of a set code, or a number matched against catalog IDs, Konami IDs, passcodes,
        /// and aliases. Empty lists every card.
        /// </summary>
        public string? Query { get; init; }

        /// <summary>Only cards with this status; null for both.</summary>
        public CatalogStatus? Status { get; init; }
    }
}
