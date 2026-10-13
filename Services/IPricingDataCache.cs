using CardCollector.DTO;

namespace CardCollector.Services
{
    /// <summary>
    /// Holds TCGPlayer prices (crawled from tcgcsv.com's catalog mirror) in memory, indexed so a card's prices can be found
    /// without a live HTTP call per card.
    /// </summary>
    public interface IPricingDataCache
    {
        /// <summary>
        /// Returns the known set printings (code, rarity, edition, price) for the given card, or an empty list if not found.
        /// </summary>
        IReadOnlyList<TCGPriceSet> GetCardSets(int cardID);

        /// <summary>
        /// Rebuilds the pricing index from the price data currently loaded, without fetching anything.
        /// </summary>
        void RebuildIndex();
    }
}
