using CardCollector.Data.Models;

namespace CardCollector.Services
{
    /// <summary>
    /// Fetches live TCGPlayer pricing for card printings from the YGOProDeck pricing endpoint.
    /// </summary>
    public interface IPricingService
    {
        /// <summary>
        /// Returns every (set code, rarity name) printing of the given card mapped to its set of listed editions,
        /// fetched from a single live API call. Returns an empty map if the card or its printings can't be fetched.
        /// </summary>
        Task<IReadOnlyDictionary<(string SetCode, string RarityName), IReadOnlySet<CardEdition>>> GetCardEditionMapAsync(int cardID);

        /// <summary>
        /// Returns the current TCGPlayer market price for the specified card printing, or null if unavailable.
        /// When <paramref name="edition"/> is provided, matches the printing with that specific edition first,
        /// falling back to a set/rarity-only match if no edition-qualified printing is found.
        /// </summary>
        Task<decimal?> GetPrintingPriceAsync(int cardID, string setCode, string rarityName, CardEdition? edition = null);
    }
}
