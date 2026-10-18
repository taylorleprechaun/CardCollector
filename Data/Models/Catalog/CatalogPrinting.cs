namespace CardCollector.Data.Models.Catalog
{
    /// <summary>
    /// One sellable printing of a catalog card: a set code, a rarity, and optionally a print variant. <see cref="ID"/> is
    /// permanent; collection rows point at it instead of copying the set code, rarity, and variant.
    /// </summary>
    public sealed class CatalogPrinting
    {
        public CatalogCard? Card { get; set; }

        public int CatalogCardID { get; set; }

        public int CatalogSetID { get; set; }

        public DateTime DateCreated { get; set; }

        public DateTime DateModified { get; set; }

        public DateTime FirstSeenUTC { get; set; }

        public int ID { get; set; }

        public DateTime LastSeenUTC { get; set; }

        /// <summary>Comma-separated names of fields edited by hand. A sync never overwrites a locked field.</summary>
        public string? LockedFields { get; set; }

        /// <summary>TCGPlayer's qualifier for a distinct product of the same rarity (e.g. "Extended Art"); null for the plain print.</summary>
        public string? PrintVariant { get; set; }

        /// <summary>Normalized through <c>RarityExtensions.NormalizeRarityName</c>.</summary>
        public string RarityName { get; set; } = string.Empty;

        public CatalogSet? Set { get; set; }

        public string SetCode { get; set; } = string.Empty;

        /// <summary>The set name the card sources give this printing, which can differ from <see cref="CatalogSet.Name"/>.</summary>
        public string? SetName { get; set; }

        public ICollection<CatalogPrintingSource> Sources { get; set; } = new List<CatalogPrintingSource>();

        public CatalogStatus Status { get; set; }
    }
}
