using CardCollector.Data.Models;

namespace CardCollector.ViewModels
{
    /// <summary>A deck with its three sections resolved to cards, and the events that used it.</summary>
    public sealed class DeckDetailViewModel
    {
        /// <summary>Every card in the deck: main, then extra, then side.</summary>
        public IReadOnlyList<DeckCardViewModel> AllCards => [.. Main.Cards, .. Extra.Cards, .. Side.Cards];

        public required Deck Deck { get; init; }

        /// <summary>Events linked to this deck, newest first.</summary>
        public required IReadOnlyList<Event> Events { get; init; }

        public required DeckSectionViewModel Extra { get; init; }

        public required DeckSectionViewModel Main { get; init; }

        public required DeckTypeCounts MainTypes { get; init; }

        public required DeckSectionViewModel Side { get; init; }

        /// <summary>Copies, across all sections, of passcodes the card data doesn't know.</summary>
        public int UnknownCardCount => AllCards.Where(c => c.IsUnknown).Sum(c => c.Quantity);
    }
}
