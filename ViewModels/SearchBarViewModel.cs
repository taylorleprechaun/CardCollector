namespace CardCollector.ViewModels
{
    public record SearchBarViewModel(string Placeholder, string? AutocompleteUrl, int PageSize, string? Query);
}
