using CardCollector.Data.Models.Catalog;

namespace CardCollector.Models.Catalog
{
    /// <summary>What the sync rules changed and found in one run. Each rule adds to the same instance.</summary>
    public sealed class CatalogDiffResult
    {
        public int CardsAdded { get; set; }

        public int CardsUpdated { get; set; }

        public ICollection<DetectedReviewItem> Items { get; } = new List<DetectedReviewItem>();

        /// <summary>Cards created this run; they need adding to the database (their printings and aliases come along).</summary>
        public ICollection<CatalogCard> NewCards { get; } = new List<CatalogCard>();

        /// <summary>Sets created this run; they need adding to the database.</summary>
        public ICollection<CatalogSet> NewSets { get; } = new List<CatalogSet>();

        public int PrintingsAdded { get; set; }

        public int ProductsIgnored { get; set; }

        public int ProductsLinked { get; set; }

        public int ProductsUnmapped { get; set; }
    }
}
