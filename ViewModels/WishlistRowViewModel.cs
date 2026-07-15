namespace CardCollector.ViewModels
{
    public sealed class WishlistRowViewModel
    {
        public required string FilterParams { get; init; }

        public required WishlistItemViewModel Item { get; init; }

        public required string TCGDate { get; init; }
    }
}
