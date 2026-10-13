namespace CardCollector.Data.Models.Catalog
{
    /// <summary>One catalog sync: what each source returned and what the run changed.</summary>
    public sealed class CatalogSyncRun
    {
        public int CardsAdded { get; set; }

        public int CardsUpdated { get; set; }

        public DateTime DateCreated { get; set; }

        public DateTime DateModified { get; set; }

        public DateTime? FinishedUTC { get; set; }

        public int ID { get; set; }

        /// <summary>True for the run that built the catalog from an empty database; it records no informational items.</summary>
        public bool IsBootstrap { get; set; }

        public int ItemsAutoResolved { get; set; }

        public int ItemsOpened { get; set; }

        public string? Message { get; set; }

        public int PrintingsAdded { get; set; }

        /// <summary>TCGPlayer products under a set prefix the catalog doesn't know (sealed product, tokens, other games' promos).</summary>
        public int ProductsIgnored { get; set; }

        public int ProductsLinked { get; set; }

        /// <summary>TCGPlayer products under a known set prefix that no catalog printing could take; each has an open review item.</summary>
        public int ProductsUnmapped { get; set; }

        public DateTime StartedUTC { get; set; }

        public CatalogSyncRunStatus Status { get; set; }

        public int TCGPlayerProductCount { get; set; }

        public CatalogSourceStatus? TCGPlayerStatus { get; set; }

        public int YamlYugiCardCount { get; set; }

        public CatalogSourceStatus? YamlYugiStatus { get; set; }

        public int YGOProDeckCardCount { get; set; }

        public CatalogSourceStatus? YGOProDeckStatus { get; set; }
    }
}
