namespace CardCollector.ViewModels
{
    public sealed class CartLineRowViewModel
    {
        public required int Index { get; init; }

        public required PendingOrderLineViewModel Line { get; init; }
    }
}
