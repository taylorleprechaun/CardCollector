namespace CardCollector.ViewModels
{
    /// <summary>One row of the Decks list: a deck's name and how big it is and how many events used it.</summary>
    public sealed class DeckListItemViewModel
    {
        public required int EventCount { get; init; }

        public required int ExtraCount { get; init; }

        public required int ID { get; init; }

        public required int MainCount { get; init; }

        public required string Name { get; init; }

        public string? Notes { get; init; }

        public required int SideCount { get; init; }
    }
}
