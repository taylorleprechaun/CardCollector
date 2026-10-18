using CardCollector.Data.Models.Catalog;
using CardCollector.Models.Catalog;

namespace CardCollector.Repository.Catalog
{
    /// <summary>
    /// Reads and changes catalog cards, printings, and their links for hand edits, plus the sync run history. Changes are
    /// staged until <see cref="SaveChangesAsync"/>.
    /// </summary>
    public interface ICatalogAdminRepository
    {
        void AddSet(CatalogSet set);

        /// <summary>
        /// Counts the collection, wishlist, checked-out, and cart rows on each printing of the card, keyed by printing ID.
        /// Printings with none are left out.
        /// </summary>
        Task<IReadOnlyDictionary<int, int>> CountUserRowsByPrintingAsync(int catalogCardID, CancellationToken cancellationToken = default);

        /// <summary>Returns an active card other than <paramref name="exceptCardID"/> that shows this passcode; null when none does.</summary>
        Task<CatalogCard?> FindOtherCardWithPasscodeAsync(int passcode, int exceptCardID, CancellationToken cancellationToken = default);

        /// <summary>Returns the card with its aliases, images, and printings (with their sources and sets), tracked; null when it doesn't exist.</summary>
        Task<CatalogCard?> GetCardAsync(int catalogCardID, CancellationToken cancellationToken = default);

        /// <summary>Returns the printing with its card, sources, and set, tracked; null when it doesn't exist.</summary>
        Task<CatalogPrinting?> GetPrintingAsync(int catalogPrintingID, CancellationToken cancellationToken = default);

        /// <summary>Returns the printing link, tracked; null when it doesn't exist.</summary>
        Task<CatalogPrintingSource?> GetPrintingSourceAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>Returns the review item for this subject, tracked; null when there is none.</summary>
        Task<CatalogReviewItem?> GetReviewItemAsync(CatalogReviewItemKind kind, string subjectKey, CancellationToken cancellationToken = default);

        /// <summary>Returns the set with this prefix, tracked; null when there is none.</summary>
        Task<CatalogSet?> GetSetByPrefixAsync(string prefix, CancellationToken cancellationToken = default);

        /// <summary>Returns one page of sync runs, newest first, for reading only, and the total number of runs.</summary>
        Task<(IReadOnlyList<CatalogSyncRun> Items, int TotalCount)> GetSyncRunsAsync(int page, int pageSize, CancellationToken cancellationToken = default);

        /// <summary>
        /// Moves the user rows on <paramref name="from"/> to <paramref name="into"/>, folding rows that would collide on a
        /// table's one-row-per-printing rule. Staged until saved.
        /// </summary>
        Task<CatalogUserRowMoveResult> MoveUserRowsAsync(CatalogPrinting from, CatalogPrinting into, CancellationToken cancellationToken = default);

        void RemovePrintingSource(CatalogPrintingSource source);

        Task SaveChangesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns one page of cards matching the query, by name, with their images and printings, for reading only, and
        /// the total number that match.
        /// </summary>
        /// <param name="query">Part of a name, the start of a set code, or a number matched against IDs, passcodes, and aliases; empty for all.</param>
        /// <param name="status">Only cards with this status; null for both.</param>
        /// <param name="page">1-based page number.</param>
        /// <param name="pageSize">Cards per page.</param>
        /// <param name="cancellationToken">Cancels the query.</param>
        Task<(IReadOnlyList<CatalogCard> Items, int TotalCount)> SearchCardsAsync(string? query, CatalogStatus? status, int page, int pageSize, CancellationToken cancellationToken = default);
    }
}
