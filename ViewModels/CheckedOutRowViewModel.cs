namespace CardCollector.ViewModels
{
    public sealed class CheckedOutRowViewModel
    {
        public required string FilterParams { get; init; }

        public required CheckedOutCardViewModel Item { get; init; }
    }
}
