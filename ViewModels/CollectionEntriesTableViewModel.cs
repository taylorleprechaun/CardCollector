namespace CardCollector.ViewModels
{
    public sealed class CollectionEntriesTableViewModel
    {
        public string DeleteActionURL { get; init; } = string.Empty;

        public required IReadOnlyList<OrderEntryViewModel> Entries { get; init; }

        public bool ShowRemoveButton { get; init; } = true;
    }
}
