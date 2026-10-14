using CardCollector.Data.Models.Catalog;
using CardCollector.Models.Catalog;

namespace CardCollector.Rules.Catalog
{
    /// <summary>Spots sources the nightly sync keeps failing to read, so a quiet breakage doesn't go unnoticed for weeks.</summary>
    /// <remarks>
    /// A failed or skipped source leaves the catalog as it was, so nothing looks wrong in the rest of the app; only the run
    /// history shows it.
    /// </remarks>
    public static class CatalogSyncHealth
    {
        /// <summary>
        /// Returns each source that failed or was skipped in at least <paramref name="threshold"/> consecutive runs, counting
        /// back from the newest. Runs that didn't read the source at all are passed over.
        /// </summary>
        /// <param name="runsNewestFirst">Recent sync runs, newest first.</param>
        /// <param name="threshold">The streak length worth warning about; below 1 nothing is reported.</param>
        public static IReadOnlyList<CatalogSourceFailureStreak> FindFailureStreaks(IReadOnlyList<CatalogSyncRun> runsNewestFirst, int threshold)
        {
            if (runsNewestFirst is null) throw new ArgumentNullException(nameof(runsNewestFirst));
            if (threshold < 1)
                return [];

            var sources = new (CatalogSource Source, Func<CatalogSyncRun, CatalogSourceStatus?> Status)[]
            {
                (CatalogSource.TCGPlayer, r => r.TCGPlayerStatus),
                (CatalogSource.YamlYugi, r => r.YamlYugiStatus),
                (CatalogSource.YGOProDeck, r => r.YGOProDeckStatus)
            };

            var streaks = new List<CatalogSourceFailureStreak>();
            foreach (var (source, status) in sources)
            {
                var count = 0;
                DateTime? lastSucceeded = null;
                foreach (var run in runsNewestFirst)
                {
                    var runStatus = status(run);
                    if (runStatus is null)
                        continue;
                    if (runStatus == CatalogSourceStatus.Succeeded)
                    {
                        lastSucceeded = run.StartedUTC;
                        break;
                    }

                    count++;
                }

                if (count >= threshold)
                    streaks.Add(new CatalogSourceFailureStreak(source, count, lastSucceeded));
            }

            return streaks;
        }
    }
}
