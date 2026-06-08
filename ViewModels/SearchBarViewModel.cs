namespace CardCollector.ViewModels
{
    public record SearchBarViewModel(
        string? AutocompleteURL,
        int? FilterCount,
        int PageSize,
        string Placeholder,
        string? Query);
}
