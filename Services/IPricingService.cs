namespace CardCollector.Services
{
    /// <summary>
    /// Fetches live TCGPlayer pricing for card printings from the YGOProDeck pricing endpoint.
    /// </summary>
    public interface IPricingService
    {
        /// <summary>
        /// Returns the current TCGPlayer market price for the specified card printing, or null if unavailable.
        /// </summary>
        Task<decimal?> GetPrintingPriceAsync(int cardID, string setCode, string rarityName);
    }
}
