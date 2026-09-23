namespace CardCollector.ViewModels
{
    /// <summary>
    /// Filters for the Events list. <see cref="EventFilterCriteria.FormatID"/> is resolved to a date range by the
    /// service before the repository is queried; the repository only looks at the date bounds.
    /// </summary>
    public sealed class EventSearchCriteria : EventFilterCriteria
    {
        public int Page { get; init; } = 1;

        public int PageSize { get; init; } = Paging.DEFAULT_PAGE_SIZE;
    }
}
