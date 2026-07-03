using CardCollector.DTO;

namespace CardCollector.Services
{
    /// <summary>
    /// Caches the full YGOProDeck TCGPlayer pricing dataset (fetched in bulk, paginated) to disk with a TTL,
    /// so pricing lookups don't need a live HTTP call per card.
    /// </summary>
    public interface IPricingDataCache
    {
        /// <summary>
        /// Returns the known set printings (code, rarity, edition, price) for the given card, or an empty list if not found.
        /// </summary>
        IReadOnlyList<TCGPriceSet> GetCardSets(int cardID);

        /// <summary>
        /// Re-downloads the full pricing dataset regardless of cache freshness.
        /// </summary>
        Task RefreshAsync();
    }
}
