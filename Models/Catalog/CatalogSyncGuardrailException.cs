namespace CardCollector.Models.Catalog
{
    /// <summary>
    /// Thrown inside a catalog sync's transaction when the run would open more actionable review items than allowed, so the
    /// unit of work rolls back the catalog changes already saved in it.
    /// </summary>
    public sealed class CatalogSyncGuardrailException : Exception
    {
        public CatalogReviewReconcileResult Reconciled { get; }

        public CatalogSyncGuardrailException(CatalogReviewReconcileResult reconciled)
            : base("Catalog sync guardrail tripped")
        {
            Reconciled = reconciled ?? throw new ArgumentNullException(nameof(reconciled));
        }
    }
}
