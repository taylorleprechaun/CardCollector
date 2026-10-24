using CardCollector.Data.Models.Catalog;
using CardCollector.Models;
using CardCollector.ViewModels;

namespace CardCollector.Services
{
    /// <summary>
    /// The catalog review inbox: lists what the sync found (plus the legacy migration's items, kept as history) and applies
    /// the user's decisions to the catalog.
    /// </summary>
    public interface ICatalogReviewService
    {
        /// <summary>
        /// Applies what the item reports: a new passcode or name, retiring a card or printing nothing uses, or restoring a
        /// retired printing TCGPlayer still lists and linking the product to it.
        /// </summary>
        Task<SaveResult> AcceptAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>Counts the open items that need a decision, for the navigation badge.</summary>
        Task<int> CountNeedingDecisionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates the card an unmapped product names (when no catalog card has that name) with the product's printing, and
        /// links the product to it. The card starts with only a name; a source that later reports it fills in the rest.
        /// </summary>
        Task<SaveResult> CreateCardAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates the missing printing on the chosen card from the item's set code, rarity, and variant, then links the
        /// product to it.
        /// </summary>
        Task<SaveResult> CreatePrintingAsync(int id, int catalogCardID, CancellationToken cancellationToken = default);

        /// <summary>Acknowledges an informational item (or an anomaly or conflict report).</summary>
        Task<SaveResult> DismissAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Acknowledges every open informational item, narrowed to one kind and to a search the same way
        /// <see cref="SearchAsync"/> narrows them; returns how many.
        /// </summary>
        Task<int> DismissAllInformationalAsync(CatalogReviewItemKind? kind, string? query = null, CancellationToken cancellationToken = default);

        /// <summary>Returns one item as an inbox row; null when it doesn't exist.</summary>
        Task<CatalogReviewItemViewModel?> GetItemAsync(int id, string? errorMessage = null, CancellationToken cancellationToken = default);

        /// <summary>Counts open items per kind, for the inbox's kind filter.</summary>
        Task<IReadOnlyDictionary<CatalogReviewItemKind, int>> GetOpenCountsByKindAsync(CancellationToken cancellationToken = default);

        /// <summary>Links a product that matches a retired printing to that printing, leaving the printing retired.</summary>
        Task<SaveResult> LinkKeepRetiredAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>Links an unmapped product to an existing printing.</summary>
        Task<SaveResult> MapToPrintingAsync(int id, int catalogPrintingID, CancellationToken cancellationToken = default);

        /// <summary>Keeps the catalog as it is.</summary>
        Task<SaveResult> RejectAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>Puts a rejected or dismissed item back in the inbox, undecided.</summary>
        Task<SaveResult> ReopenAsync(int id, CancellationToken cancellationToken = default);

        Task<PagedResult<CatalogReviewItemViewModel>> SearchAsync(CatalogReviewSearchCriteria criteria, CancellationToken cancellationToken = default);
    }
}
