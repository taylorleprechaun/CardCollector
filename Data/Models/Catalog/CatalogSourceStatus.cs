namespace CardCollector.Data.Models.Catalog
{
    /// <summary>How one source fared in a catalog sync run.</summary>
    public enum CatalogSourceStatus
    {
        /// <summary>The source returned no usable data.</summary>
        Failed,

        /// <summary>The source returned data but far less than last time, so the run ignored it.</summary>
        Skipped,

        Succeeded
    }
}
