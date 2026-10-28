namespace CardCollector.ViewModels
{
    public sealed class OrderSearchResult
    {
        /// <summary>Rarity names across every pending order, for the Rarity filter.</summary>
        public IReadOnlyList<string> AvailableRarityNames { get; init; } = [];

        /// <summary>Set names across every pending order, for the Set filter's suggestions.</summary>
        public IReadOnlyList<string> AvailableSetNames { get; init; } = [];

        /// <summary>The page of matching orders; <see cref="PagedResult{T}.TotalQuantitySum"/> counts the cards across every match.</summary>
        public PagedResult<EditionAuditEntryViewModel> PagedItems { get; init; } = new();

        /// <summary>Purchase price times quantity across every matching order, counting an order with no price as zero.</summary>
        public decimal TotalCost { get; init; }
    }
}
