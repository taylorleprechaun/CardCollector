using CardCollector.DTO.YamlYugi;

namespace CardCollector.Repository.Sources
{
    /// <summary>
    /// Supplies yaml-yugi's card documents, kept in a local cache between downloads.
    /// </summary>
    public interface IYamlYugiSource
    {
        /// <summary>When the cache file on disk was last written; null when there is none.</summary>
        DateTime? CachedAtUTC { get; }

        /// <summary>
        /// Downloads a fresh copy when the cache is missing or stale (or always, when <paramref name="forceRefresh"/>
        /// is true) and returns the cached documents. A failed download falls back to the existing cache; with no
        /// cache at all the result is empty.
        /// </summary>
        Task<IReadOnlyList<YamlCard>> LoadAsync(bool forceRefresh);

        /// <summary>Returns the cached documents without any network access; empty when nothing is cached.</summary>
        IReadOnlyList<YamlCard> ReadCached();
    }
}
