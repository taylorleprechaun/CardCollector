namespace CardCollector.Data.Models.Catalog
{
    public enum CatalogSyncRunStatus
    {
        /// <summary>The run's changes were rolled back by a guardrail; see the run's message.</summary>
        Aborted,

        /// <summary>The run hit an error or had no usable source data.</summary>
        Failed,

        Running,

        Succeeded
    }
}
