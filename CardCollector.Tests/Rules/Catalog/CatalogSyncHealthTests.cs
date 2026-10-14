using CardCollector.Data.Models.Catalog;
using CardCollector.Rules.Catalog;

namespace CardCollector.Tests.Rules.Catalog
{
    [TestClass]
    public sealed class CatalogSyncHealthTests
    {
        [TestMethod]
        public void FindFailureStreaks_RunsThatDidNotReadSource_AreSkippedNotCounted()
        {
            IReadOnlyList<CatalogSyncRun> runs =
            [
                Run(3, tcgPlayer: CatalogSourceStatus.Failed),
                Run(2, tcgPlayer: null),
                Run(1, tcgPlayer: CatalogSourceStatus.Skipped)
            ];

            var streaks = CatalogSyncHealth.FindFailureStreaks(runs, 2);

            var streak = streaks.Single();
            Assert.AreEqual(CatalogSource.TCGPlayer, streak.Source);
            Assert.AreEqual(2, streak.RunCount);
            Assert.IsNull(streak.LastSucceededUTC);
        }

        [TestMethod]
        public void FindFailureStreaks_StreakBelowThreshold_ReportsNothing()
        {
            IReadOnlyList<CatalogSyncRun> runs =
            [
                Run(3, tcgPlayer: CatalogSourceStatus.Failed),
                Run(2, tcgPlayer: CatalogSourceStatus.Failed),
                Run(1, tcgPlayer: CatalogSourceStatus.Succeeded)
            ];

            var streaks = CatalogSyncHealth.FindFailureStreaks(runs, 3);

            Assert.AreEqual(0, streaks.Count);
        }

        [TestMethod]
        public void FindFailureStreaks_StreakReachesThreshold_ReportsCountAndLastSuccess()
        {
            IReadOnlyList<CatalogSyncRun> runs =
            [
                Run(4, tcgPlayer: CatalogSourceStatus.Failed),
                Run(3, tcgPlayer: CatalogSourceStatus.Skipped),
                Run(2, tcgPlayer: CatalogSourceStatus.Failed),
                Run(1, tcgPlayer: CatalogSourceStatus.Succeeded)
            ];

            var streaks = CatalogSyncHealth.FindFailureStreaks(runs, 3);

            var streak = streaks.Single();
            Assert.AreEqual(3, streak.RunCount);
            Assert.AreEqual(new DateTime(2026, 10, 1, 4, 0, 0, DateTimeKind.Utc), streak.LastSucceededUTC);
        }

        [TestMethod]
        public void FindFailureStreaks_ThresholdBelowOne_ReportsNothing()
        {
            IReadOnlyList<CatalogSyncRun> runs = [Run(1, tcgPlayer: CatalogSourceStatus.Failed)];

            var streaks = CatalogSyncHealth.FindFailureStreaks(runs, 0);

            Assert.AreEqual(0, streaks.Count);
        }

        private static CatalogSyncRun Run(int day, CatalogSourceStatus? tcgPlayer) => new()
        {
            StartedUTC = new DateTime(2026, 10, day, 4, 0, 0, DateTimeKind.Utc),
            TCGPlayerStatus = tcgPlayer,
            YamlYugiStatus = CatalogSourceStatus.Succeeded,
            YGOProDeckStatus = CatalogSourceStatus.Succeeded
        };
    }
}
