using CardCollector.DTO.YGOProDeck;

namespace CardCollector.Repository.Sources
{
    /// <summary>
    /// Supplies YGOProDeck's card list (card data, artwork, and cross-source identifiers), kept in a local cache
    /// between downloads.
    /// </summary>
    public interface IYGOProDeckSource
    {
        /// <summary>
        /// Downloads a fresh copy when the cache is missing or stale (or always, when <paramref name="forceRefresh"/>
        /// is true) and returns the cached cards. A failed download falls back to the existing cache; with no cache
        /// at all the result is empty.
        /// </summary>
        Task<IReadOnlyList<YGOProDeckCard>> LoadAsync(bool forceRefresh);

        /// <summary>Returns the cached cards without any network access; empty when nothing is cached.</summary>
        IReadOnlyList<YGOProDeckCard> ReadCached();
    }
}
