namespace CardCollector.ViewModels
{
    /// <summary>The cards of one section of a deck, in display order (by type, then name).</summary>
    public sealed class DeckSectionViewModel
    {
        public required IReadOnlyList<DeckCardViewModel> Cards { get; init; }

        /// <summary>The number of cards including copies.</summary>
        public int Count => Cards.Sum(c => c.Quantity);
    }
}
