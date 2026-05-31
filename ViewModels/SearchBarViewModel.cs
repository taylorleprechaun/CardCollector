namespace CardCollector.ViewModels
{
    public record SearchBarViewModel(string? AutocompleteURL, int PageSize, string Placeholder, string? Query);
}
