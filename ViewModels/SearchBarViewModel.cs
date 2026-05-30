namespace CardCollector.ViewModels
{
    public record SearchBarViewModel(string Placeholder, string? AutocompleteURL, int PageSize, string? Query);
}
