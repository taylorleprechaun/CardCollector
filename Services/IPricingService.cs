using CardCollector.Data.Models;

namespace CardCollector.Services
{
    /// <summary>
    /// Fetches live TCGPlayer pricing for card printings from the cached pricing data.
    /// </summary>
    public interface IPricingService
    {
        /// <summary>
        /// Returns every (set code, rarity name, print variant) printing of the given card mapped to its set of
        /// listed editions, fetched from a single live API call. Returns an empty map if the card or its
        /// printings can't be fetched.
        /// </summary>
        Task<IReadOnlyDictionary<(string SetCode, string RarityName, string? PrintVariant), IReadOnlySet<CardEdition>>> GetCardEditionMapAsync(int cardID);

        /// <summary>
        /// Returns the current TCGPlayer market price for the specified card printing, or null if unavailable.
        /// <paramref name="printVariant"/> distinguishes a base print (null) from a distinct sellable variant of
        /// the same rarity (e.g. "Extended Art"). When <paramref name="edition"/> is provided, matches the
        /// printing with that specific edition first, falling back to a set/rarity/variant-only match if no
        /// edition-qualified printing is found.
        /// </summary>
        Task<decimal?> GetPrintingPriceAsync(int cardID, string setCode, string rarityName, CardEdition? edition = null, string? printVariant = null);
    }
}
