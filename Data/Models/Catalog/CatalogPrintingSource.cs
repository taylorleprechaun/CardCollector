namespace CardCollector.Data.Models.Catalog
{
    /// <summary>
    /// Links a printing to an outside listing of it. For TCGPlayer the key is the product id, which is how prices reach the
    /// printing; it never changes even when TCGPlayer edits the product's name.
    /// </summary>
    public sealed class CatalogPrintingSource
    {
        public int CatalogPrintingID { get; set; }

        public DateTime DateCreated { get; set; }

        public DateTime DateModified { get; set; }

        /// <summary>The outside listing's id (a TCGPlayer product id, as text).</summary>
        public string ExternalKey { get; set; } = string.Empty;

        /// <summary>The listing's name, set code, rarity, and variant when last seen, used to notice edits to the listing.</summary>
        public string? ExternalSummary { get; set; }

        public DateTime FirstSeenUTC { get; set; }

        public int ID { get; set; }

        public DateTime LastSeenUTC { get; set; }

        public CatalogSource Source { get; set; }
    }
}
