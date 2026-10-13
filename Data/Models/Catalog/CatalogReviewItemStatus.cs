namespace CardCollector.Data.Models.Catalog
{
    public enum CatalogReviewItemStatus
    {
        /// <summary>The user applied the reported change.</summary>
        Accepted,

        /// <summary>The user acknowledged an informational item.</summary>
        Dismissed,

        Open,

        /// <summary>The user kept the catalog as it was. Reopens only if the source reports something different.</summary>
        Rejected,

        /// <summary>The source stopped reporting the problem before anyone acted on it.</summary>
        ResolvedBySource
    }
}
