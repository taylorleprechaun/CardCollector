namespace CardCollector.ViewModels
{
    public sealed class CardPriceHistorySeries
    {
        public List<string> Dates { get; init; } = [];
        public string Label { get; init; } = string.Empty;
        public List<decimal> Values { get; init; } = [];
    }
}
