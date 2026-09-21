namespace CardCollector.ViewModels
{
    /// <summary>How many of a section's cards are monsters, spells and traps.</summary>
    public sealed record DeckTypeCounts(int Monsters, int Spells, int Traps);
}
