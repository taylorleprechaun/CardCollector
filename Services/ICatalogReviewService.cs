using CardCollector.Data.Models.Catalog;
using CardCollector.Models;
using CardCollector.ViewModels;

namespace CardCollector.Services
{
    /// <summary>
    /// The catalog review inbox: lists what the sync and the legacy migration found, and applies the user's decisions to the
    /// catalog (and, for unmapped legacy rows, to the collection).
    /// </summary>
    public interface ICatalogReviewService
    {
        /// <summary>Applies what the item reports: a new passcode or name, or retiring a card or printing nothing uses.</summary>
        Task<SaveResult> AcceptAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>Counts the open items that need a decision, for the navigation badge.</summary>
        Task<int> CountNeedingDecisionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates the missing printing on the chosen card from the item's set code, rarity, and variant, then links the
        /// product to it or restores the legacy row on it.
        /// </summary>
        Task<SaveResult> CreatePrintingAsync(int id, int catalogCardID, CancellationToken cancellationToken = default);

        /// <summary>Acknowledges an informational item (or an anomaly or conflict report).</summary>
        Task<SaveResult> DismissAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>Acknowledges every open informational item, or only those of one kind; returns how many.</summary>
        Task<int> DismissAllInformationalAsync(CatalogReviewItemKind? kind, CancellationToken cancellationToken = default);

        /// <summary>Returns one item as an inbox row; null when it doesn't exist.</summary>
        Task<CatalogReviewItemViewModel?> GetItemAsync(int id, string? errorMessage = null, CancellationToken cancellationToken = default);

        /// <summary>Counts open items per kind, for the inbox's kind filter.</summary>
        Task<IReadOnlyDictionary<CatalogReviewItemKind, int>> GetOpenCountsByKindAsync(CancellationToken cancellationToken = default);

        /// <summary>Links an unmapped product to an existing printing, or restores an unmapped legacy row on it.</summary>
        Task<SaveResult> MapToPrintingAsync(int id, int catalogPrintingID, CancellationToken cancellationToken = default);

        /// <summary>Keeps the catalog as it is (for an unmapped legacy row, discards the row).</summary>
        Task<SaveResult> RejectAsync(int id, CancellationToken cancellationToken = default);

        Task<PagedResult<CatalogReviewItemViewModel>> SearchAsync(CatalogReviewSearchCriteria criteria, CancellationToken cancellationToken = default);
    }
}
