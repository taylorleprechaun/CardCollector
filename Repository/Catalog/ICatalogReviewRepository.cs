using CardCollector.Data.Models.Catalog;

namespace CardCollector.Repository.Catalog
{
    /// <summary>
    /// Reads and changes review items and the catalog rows a review decision touches. Changes are staged until
    /// <see cref="SaveChangesAsync"/>.
    /// </summary>
    public interface ICatalogReviewRepository
    {
        void AddCard(CatalogCard card);

        void AddSet(CatalogSet set);

        /// <summary>Counts open items of the given kinds.</summary>
        Task<int> CountOpenAsync(IReadOnlyCollection<CatalogReviewItemKind> kinds, CancellationToken cancellationToken = default);

        /// <summary>Counts open items per kind; kinds with none are left out.</summary>
        Task<IReadOnlyDictionary<CatalogReviewItemKind, int>> CountOpenByKindAsync(CancellationToken cancellationToken = default);

        /// <summary>Counts the collection, wishlist, checked-out, and cart rows that point at any printing of the card.</summary>
        Task<int> CountUserRowsForCardAsync(int catalogCardID, CancellationToken cancellationToken = default);

        /// <summary>Counts the collection, wishlist, checked-out, and cart rows that point at the printing.</summary>
        Task<int> CountUserRowsForPrintingAsync(int catalogPrintingID, CancellationToken cancellationToken = default);

        /// <summary>Returns a card (active or retired) with exactly this name, ignoring case, for reading only; null when there is none.</summary>
        Task<CatalogCard?> FindCardByNameAsync(string name, CancellationToken cancellationToken = default);

        /// <summary>Returns the card with its printings (and their sources and sets), tracked; null when it doesn't exist.</summary>
        Task<CatalogCard?> GetCardAsync(int catalogCardID, CancellationToken cancellationToken = default);

        /// <summary>Returns the item, tracked; null when it doesn't exist.</summary>
        Task<CatalogReviewItem?> GetItemAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns every open item of the given kinds, tracked; with <paramref name="query"/>, only those whose summary,
        /// resolution, or card name contains it.
        /// </summary>
        Task<IReadOnlyList<CatalogReviewItem>> GetOpenItemsAsync(IReadOnlyCollection<CatalogReviewItemKind> kinds, string? query = null, CancellationToken cancellationToken = default);

        /// <summary>Returns the printing with its card, sources, and set, tracked; null when it doesn't exist.</summary>
        Task<CatalogPrinting?> GetPrintingAsync(int catalogPrintingID, CancellationToken cancellationToken = default);

        /// <summary>Returns the set with this prefix, tracked; null when there is none.</summary>
        Task<CatalogSet?> GetSetByPrefixAsync(string prefix, CancellationToken cancellationToken = default);

        /// <summary>True when some printing already lists this source key (e.g. a TCGPlayer product id).</summary>
        Task<bool> IsSourceLinkedAsync(CatalogSource source, string externalKey, CancellationToken cancellationToken = default);

        Task SaveChangesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns one page of items with the given statuses and kinds (and, with <paramref name="query"/>, whose summary,
        /// resolution, or card name contains it), most recently detected first, for reading only, and the total number that match.
        /// </summary>
        Task<(IReadOnlyList<CatalogReviewItem> Items, int TotalCount)> SearchAsync(
            IReadOnlyCollection<CatalogReviewItemStatus> statuses,
            IReadOnlyCollection<CatalogReviewItemKind> kinds,
            string? query,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default);
    }
}
