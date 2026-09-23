namespace CardCollector.ViewModels
{
    /// <summary>A deck as a choice in a picker: just enough to list and select it.</summary>
    public sealed record DeckOption(int ID, string Name);
}
