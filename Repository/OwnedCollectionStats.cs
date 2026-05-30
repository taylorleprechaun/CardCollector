namespace CardCollector.Repository
{
    public record OwnedCollectionStats(
        int TotalQuantity,
        decimal? MarketValueAtEntry,
        decimal? TotalSpent);
}
