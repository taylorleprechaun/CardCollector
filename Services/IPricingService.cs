using CardCollector.Data.Models;

namespace CardCollector.Services
{
    /// <summary>
    /// Fetches live TCGPlayer pricing for card printings from the cached pricing data.
    /// </summary>
    public interface IPricingService
    {
        /// <summary>
        /// Returns the editions TCGPlayer lists for each of the card's printings, keyed by catalog printing ID. A printing
        /// with no linked TCGPlayer product is left out.
        /// </summary>
        Task<IReadOnlyDictionary<int, IReadOnlySet<CardEdition>>> GetEditionsByPrintingAsync(int cardID);

        /// <summary>
        /// Returns the current TCGPlayer market price of a catalog printing, or null if unavailable. When
        /// <paramref name="edition"/> is provided, that edition's listing is preferred, falling back to any edition.
        /// </summary>
        Task<decimal?> GetPrintingPriceAsync(int printingID, CardEdition? edition = null);
    }
}
