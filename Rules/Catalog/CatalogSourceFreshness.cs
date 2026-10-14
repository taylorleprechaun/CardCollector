using CardCollector.Data.Models.Catalog;

namespace CardCollector.Rules.Catalog
{
    /// <summary>Decides whether a start-up sync has anything to do.</summary>
    /// <remarks>
    /// The catalog only changes when a source's data does. When every cache file was written before the last successful
    /// run finished, that run already processed exactly this data, and running again would only re-touch every open
    /// review item (inflating its detection count) and keep the app busy for no change.
    /// </remarks>
    public static class CatalogSourceFreshness
    {
        /// <summary>
        /// True when <paramref name="lastSucceeded"/> finished after every cache in <paramref name="cachesWrittenUTC"/>
        /// was written. A missing cache timestamp, or no successful run, means the data must be synced.
        /// </summary>
        public static bool IsAlreadySynced(CatalogSyncRun? lastSucceeded, IReadOnlyCollection<DateTime?> cachesWrittenUTC)
        {
            if (cachesWrittenUTC is null) throw new ArgumentNullException(nameof(cachesWrittenUTC));

            if (lastSucceeded?.FinishedUTC is not DateTime finishedUTC || cachesWrittenUTC.Count == 0)
                return false;

            return cachesWrittenUTC.All(written => written is DateTime writtenUTC && writtenUTC <= finishedUTC);
        }
    }
}
