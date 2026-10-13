namespace CardCollector.Data.Models.Catalog
{
    /// <summary>A product line identified by its set code prefix (e.g. "LOB").</summary>
    public sealed class CatalogSet
    {
        public DateTime DateCreated { get; set; }

        public DateTime DateModified { get; set; }

        public int ID { get; set; }

        /// <summary>The shortest name any printing gives the set, since longer variants tend to be promo names.</summary>
        public string Name { get; set; } = string.Empty;

        public string Prefix { get; set; } = string.Empty;

        /// <summary>"yyyy-MM-dd", from YGOProDeck's set list. Null when unknown.</summary>
        public string? TCGReleaseDate { get; set; }
    }
}
