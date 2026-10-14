namespace CardCollector.Models.Catalog
{
    /// <summary>Guardrails for the catalog sync, bound from the "CatalogSync" configuration section.</summary>
    public sealed class CatalogSyncSettings
    {
        /// <summary>
        /// A run that would open more new actionable review items than this is rolled back and recorded as one sync
        /// anomaly instead. The bootstrap run is exempt.
        /// </summary>
        public int MaxNewActionableItems { get; set; } = 250;

        /// <summary>
        /// A source returning fewer records than this fraction of its count in the last successful run is ignored for the
        /// run, on the assumption the download was partial.
        /// </summary>
        public double MinSourceRatio { get; set; } = 0.9;

        /// <summary>The sync history page warns about a source once it has failed or been skipped this many runs in a row.</summary>
        public int SourceFailureWarningRuns { get; set; } = 3;
    }
}
