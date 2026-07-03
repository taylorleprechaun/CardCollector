namespace CardCollector.ViewModels
{
    public sealed class CollectionGroupRowViewModel
    {
        public required string FilterParams { get; init; }

        public required CollectionGroupViewModel Group { get; init; }

        public required string TCGDate { get; init; }
    }
}
