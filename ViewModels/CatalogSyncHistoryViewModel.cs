using CardCollector.Models.Catalog;

namespace CardCollector.ViewModels
{
    /// <summary>A page of catalog sync runs plus the sources that keep failing.</summary>
    public sealed class CatalogSyncHistoryViewModel
    {
        /// <summary>Sources that failed or were skipped in at least <see cref="WarningRuns"/> runs in a row.</summary>
        public IReadOnlyList<CatalogSourceFailureStreak> FailureStreaks { get; init; } = [];

        public PagedResult<CatalogSyncRunViewModel> Runs { get; init; } = new();

        /// <summary>The streak length that triggers a warning.</summary>
        public int WarningRuns { get; init; }
    }
}
