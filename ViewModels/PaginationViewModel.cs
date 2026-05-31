namespace CardCollector.ViewModels
{
    public sealed class PaginationViewModel
    {
        public IDictionary<string, string?>? AdditionalParams { get; init; }
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
