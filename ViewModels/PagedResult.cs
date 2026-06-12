namespace CardCollector.ViewModels
{
    public sealed class PagedResult<T>
    {
        public bool HasNextPage => Page < TotalPages;
        public bool HasPreviousPage => Page > 1;
        public IReadOnlyList<T> Items { get; init; } = [];
        public int Page { get; init; }
        public int PageSize { get; init; }
        public int TotalCount { get; init; }
        public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);
        /// <summary>Optional sum of a quantity field across all filtered results (not just the current page). Zero when unused.</summary>
        public int TotalQuantitySum { get; init; }
    }
}
