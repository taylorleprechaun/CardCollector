using CardCollector.Data.Models.Catalog;

namespace CardCollector.ViewModels
{
    /// <summary>One catalog sync run as the history page shows it.</summary>
    public sealed class CatalogSyncRunViewModel
    {
        public int CardsAdded { get; init; }

        public int CardsUpdated { get; init; }

        public DateTime? FinishedUTC { get; init; }

        public int ID { get; init; }

        public bool IsBootstrap { get; init; }

        public int ItemsAutoResolved { get; init; }

        public int ItemsOpened { get; init; }

        public string? Message { get; init; }

        public int PrintingsAdded { get; init; }

        public int ProductsIgnored { get; init; }

        public int ProductsLinked { get; init; }

        public int ProductsUnmapped { get; init; }

        public IReadOnlyList<CatalogSyncSourceResultViewModel> Sources { get; init; } = [];

        public DateTime StartedUTC { get; init; }

        public CatalogSyncRunStatus Status { get; init; }
    }
}
