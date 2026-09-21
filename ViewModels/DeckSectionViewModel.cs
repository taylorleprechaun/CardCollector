namespace CardCollector.ViewModels
{
    /// <summary>The cards of one section of a deck, in the order they were arranged.</summary>
    public sealed class DeckSectionViewModel
    {
        public required IReadOnlyList<DeckCardViewModel> Cards { get; init; }

        /// <summary>The number of cards including copies.</summary>
        public int Count => Cards.Sum(c => c.Quantity);
    }
}
