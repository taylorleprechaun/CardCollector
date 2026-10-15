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
        /// Returns the price rows (one per product and edition) of the TCGPlayer products linked to a catalog printing;
        /// empty when the printing is unknown or has no linked product.
        /// </summary>
        IReadOnlyList<TCGPriceSet> GetPrintingRows(int printingID);

        /// <summary>Returns the price rows of each of the card's printings, keyed by catalog printing ID; printings without rows are left out.</summary>
        IReadOnlyDictionary<int, IReadOnlyList<TCGPriceSet>> GetPrintingRowsByCard(int cardID);

        /// <summary>
        /// Rebuilds the pricing index from the price data currently loaded, without fetching anything.
        /// </summary>
        void RebuildIndex();
    }
}
