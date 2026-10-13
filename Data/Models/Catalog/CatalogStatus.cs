namespace CardCollector.Data.Models.Catalog
{
    /// <summary>Whether a catalog card or printing is in use. Catalog rows are retired, never deleted.</summary>
    public enum CatalogStatus
    {
        Active,

        Retired
    }
}
