using CardCollector.Data.Models.Catalog;
using CardCollector.Models;
using CardCollector.ViewModels;

namespace CardCollector.Services
{
    /// <summary>
    /// Hand edits to the card catalog: card fields and passcodes, printings added, merged, or retired, TCGPlayer links
    /// removed, and the sync run history. Every change is kept by later syncs.
    /// </summary>
    public interface ICatalogAdminService
    {
        /// <summary>Adds a printing the sources don't report; the sync won't flag it as missing.</summary>
        Task<SaveResult> AddPrintingAsync(int catalogCardID, CatalogPrintingInput input, CancellationToken cancellationToken = default);

        /// <summary>Returns the card's admin view; null when it doesn't exist.</summary>
        Task<CatalogCardDetailsViewModel?> GetCardAsync(int catalogCardID, CancellationToken cancellationToken = default);

        /// <summary>Returns one page of sync runs, newest first, with the sources that keep failing.</summary>
        Task<CatalogSyncHistoryViewModel> GetSyncHistoryAsync(int page, int pageSize, CancellationToken cancellationToken = default);

        /// <summary>
        /// Moves every collection, wishlist, checked-out, cart, and dismissed row and every source link from one printing to
        /// another printing of the same card, then retires the first.
        /// </summary>
        Task<SaveResult> MergePrintingAsync(int fromPrintingID, int intoPrintingID, CancellationToken cancellationToken = default);

        /// <summary>Returns one page of catalog cards matching the criteria.</summary>
        Task<PagedResult<CatalogCardRowViewModel>> SearchCardsAsync(CatalogCardSearchCriteria criteria, CancellationToken cancellationToken = default);

        /// <summary>Retires or restores a card. A card still used by any of your rows can't be retired.</summary>
        Task<SaveResult> SetCardStatusAsync(int catalogCardID, CatalogStatus status, CancellationToken cancellationToken = default);

        /// <summary>Retires or restores a printing. A printing still used by any of your rows can't be retired.</summary>
        Task<SaveResult> SetPrintingStatusAsync(int catalogPrintingID, CatalogStatus status, CancellationToken cancellationToken = default);

        /// <summary>Removes a TCGPlayer product's link to a printing, so its prices stop applying there.</summary>
        Task<SaveResult> UnlinkSourceAsync(int printingSourceID, CancellationToken cancellationToken = default);

        /// <summary>Lets the sync update a field edited by hand again.</summary>
        Task<SaveResult> UnlockFieldAsync(int catalogCardID, string fieldName, CancellationToken cancellationToken = default);

        /// <summary>Saves hand edits to a card; every changed field is locked against the sync.</summary>
        Task<SaveResult> UpdateCardAsync(int catalogCardID, CatalogCardEditInput input, CancellationToken cancellationToken = default);
    }
}
