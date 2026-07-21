namespace CardCollector.ViewModels
{
    public class SearchFormViewModel
    {
        public int ActiveFilterCount { get; init; }
        public string? AutocompleteURL { get; init; }
        public string ClearFiltersURL { get; init; } = string.Empty;
        public IDictionary<string, string>? ExtraHiddenInputs { get; init; }
        public AdvancedFiltersViewModel? Filters { get; init; }
        public bool HasActiveFilters { get; init; }
        public int PageSize { get; init; }
        public string Placeholder { get; init; } = "Search…";
        public string? Query { get; init; }
    }
}
