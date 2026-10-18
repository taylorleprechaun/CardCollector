namespace CardCollector.ViewModels
{
    /// <summary>A card the Stats page can chart price history for, under its current catalog name.</summary>
    public sealed class PriceHistoryCardViewModel
    {
        public required int CardID { get; init; }

        public string? ImageURL { get; init; }

        public required string Name { get; init; }
    }
}
