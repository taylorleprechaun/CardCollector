using CardCollector.Data.Models.Catalog;

namespace CardCollector.ViewModels
{
    /// <summary>An outside listing linked to a catalog printing.</summary>
    public sealed class CatalogPrintingSourceViewModel
    {
        /// <summary>True for links the admin page may remove (TCGPlayer products).</summary>
        public bool CanUnlink { get; init; }

        public string ExternalKey { get; init; } = string.Empty;

        /// <summary>The listing's name, set code, rarity, and variant when last seen.</summary>
        public string? ExternalSummary { get; init; }

        public int ID { get; init; }

        public DateTime LastSeenUTC { get; init; }

        public CatalogSource Source { get; init; }
    }
}
