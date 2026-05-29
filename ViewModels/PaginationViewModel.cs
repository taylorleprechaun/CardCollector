namespace CardCollector.ViewModels
{
    public class PaginationViewModel
    {
        public required string AriaLabel { get; init; }
        public bool HasNextPage { get; init; }
        public bool HasPreviousPage { get; init; }
        public required string PageName { get; init; }
        public int PageNumber { get; init; }
        public int PageSize { get; init; }
        public string? Query { get; init; }
        public int TotalPages { get; init; }
    }
}
