namespace CardCollector.Data.Models.Catalog
{
    /// <summary>
    /// An outside identifier that has pointed at a catalog card. Kept after the source stops reporting it, so old deck
    /// lists and old collection rows still resolve; when several cards share a value, the most recently seen one wins.
    /// </summary>
    public sealed class CatalogCardAlias
    {
        public int CatalogCardID { get; set; }

        public DateTime DateCreated { get; set; }

        public DateTime DateModified { get; set; }

        public DateTime FirstSeenUTC { get; set; }

        public int ID { get; set; }

        public CatalogAliasKind Kind { get; set; }

        public DateTime LastSeenUTC { get; set; }

        public CatalogSource Source { get; set; }

        public int Value { get; set; }
    }
}
