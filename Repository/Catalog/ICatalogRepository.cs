using CardCollector.Data.Models.Catalog;
using CardCollector.Models.Catalog;

namespace CardCollector.Repository.Catalog
{
    /// <summary>
    /// Reads and writes the card catalog for a sync run. Adds are staged until <see cref="SaveChangesAsync"/>, so a run's
    /// changes can be saved together inside one transaction.
    /// </summary>
    public interface ICatalogRepository
    {
        /// <summary>Stages new cards, together with their aliases, images, printings, and printing sources.</summary>
        void AddCards(IEnumerable<CatalogCard> cards);

        /// <summary>Stages new review items.</summary>
        void AddReviewItems(IEnumerable<CatalogReviewItem> items);

        /// <summary>Stages new sets.</summary>
        void AddSets(IEnumerable<CatalogSet> sets);

        /// <summary>Saves a new sync run immediately, so a run that later fails is still recorded.</summary>
        Task AddSyncRunAsync(CatalogSyncRun run, CancellationToken cancellationToken = default);

        /// <summary>Drops every staged change and stops tracking everything loaded so far.</summary>
        void DiscardChanges();

        /// <summary>Returns the most recent sync runs, newest first.</summary>
        Task<IReadOnlyList<CatalogSyncRun>> GetRecentSyncRunsAsync(int count, CancellationToken cancellationToken = default);

        /// <summary>Returns every review item, tracked so changes to them are saved.</summary>
        Task<IReadOnlyList<CatalogReviewItem>> GetReviewItemsAsync(CancellationToken cancellationToken = default);

        /// <summary>Loads the whole catalog (cards with aliases, images, printings, and their sources and sets), tracked.</summary>
        Task<CatalogState> LoadStateAsync(CancellationToken cancellationToken = default);

        Task SaveChangesAsync(CancellationToken cancellationToken = default);

        /// <summary>Saves the sync run's current values, even after <see cref="DiscardChanges"/>.</summary>
        Task UpdateSyncRunAsync(CatalogSyncRun run, CancellationToken cancellationToken = default);
    }
}
