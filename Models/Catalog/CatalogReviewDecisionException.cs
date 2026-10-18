namespace CardCollector.Models.Catalog
{
    /// <summary>
    /// Thrown inside a review decision's transaction when the decision can't be completed, so the unit of work rolls back
    /// anything the decision already saved (e.g. a printing it created). Carries the result to report.
    /// </summary>
    public sealed class CatalogReviewDecisionException : Exception
    {
        public SaveResult Result { get; }

        public CatalogReviewDecisionException(SaveResult result) : base(string.Join(" ", result?.Errors ?? []))
        {
            Result = result ?? throw new ArgumentNullException(nameof(result));
        }
    }
}
