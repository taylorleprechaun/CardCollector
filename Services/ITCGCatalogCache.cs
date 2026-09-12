using CardCollector.DTO;

namespace CardCollector.Services
{
    /// <summary>
    /// Caches TCGPlayer's product catalog (crawled from tcgcsv.com), exposing every known printing — set code,
    /// rarity, edition, print variant (e.g. Extended Art), card name, and market price. This is the single
    /// source both <see cref="IPricingDataCache"/> (price lookups) and the card catalog (rarity/print-variant
    /// enrichment) consume, since tcgcsv.com tracks actual sellable SKUs that neither yaml-yugi nor YGOProDeck's
    /// own card data distinguishes.
    /// </summary>
    public interface ITCGCatalogCache
    {
        /// <summary>Returns every known printing in the catalog.</summary>
        IReadOnlyList<TCGPriceSet> GetAllPrintings();

        /// <summary>
        /// Fetches a fresh catalog if the on-disk cache is missing or stale; otherwise a no-op. Unlike
        /// <see cref="RefreshAsync"/>, this respects the cache TTL.
        /// </summary>
        Task LoadIfStaleAsync();

        /// <summary>Forces a fresh crawl of tcgcsv.com, ignoring cache freshness.</summary>
        Task RefreshAsync();
    }
}
