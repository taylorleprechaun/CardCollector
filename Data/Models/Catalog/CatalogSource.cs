namespace CardCollector.Data.Models.Catalog
{
    /// <summary>Where a piece of catalog data came from.</summary>
    public enum CatalogSource
    {
        /// <summary>Entered by hand in the catalog admin pages.</summary>
        Manual,

        /// <summary>TCGPlayer's catalog, read through tcgcsv.</summary>
        TCGPlayer,

        YamlYugi,

        YGOProDeck
    }
}
