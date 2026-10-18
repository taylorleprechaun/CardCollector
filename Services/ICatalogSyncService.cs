using CardCollector.Models.Catalog;

namespace CardCollector.Services
{
    /// <summary>
    /// Brings the card catalog up to date with the outside sources. New cards and printings are added; changes to existing
    /// ones that need a decision are left for the review inbox.
    /// </summary>
    public interface ICatalogSyncService
    {
        /// <summary>Runs one sync and returns what it did. Failures are recorded on the returned run rather than thrown.</summary>
        Task<CatalogSyncResult> SyncAsync(CatalogSourceMode sourceMode, CancellationToken cancellationToken = default);
    }
}
