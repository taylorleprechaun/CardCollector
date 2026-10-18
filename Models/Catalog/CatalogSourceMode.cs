namespace CardCollector.Models.Catalog
{
    /// <summary>How a catalog sync gets its source data.</summary>
    public enum CatalogSourceMode
    {
        /// <summary>Use only the cache files already on disk; no network access.</summary>
        CacheOnly,

        /// <summary>Download every source again regardless of cache age (the nightly refresh).</summary>
        Force,

        /// <summary>Download only the sources whose cache is missing or stale.</summary>
        IfStale
    }
}
