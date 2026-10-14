using CardCollector.Data.Models.Catalog;
using CardCollector.Rules.Catalog;

namespace CardCollector.Tests.Rules.Catalog
{
    [TestClass]
    public sealed class CatalogSourceFreshnessTests
    {
        private static readonly DateTime FinishedUTC = new(2026, 10, 14, 21, 32, 47, DateTimeKind.Utc);

        [TestMethod]
        public void IsAlreadySynced_AllCachesWrittenBeforeLastRunFinished_ReturnsTrue()
        {
            var result = CatalogSourceFreshness.IsAlreadySynced(Run(FinishedUTC), [FinishedUTC.AddDays(-2), FinishedUTC.AddSeconds(-20), FinishedUTC]);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsAlreadySynced_CacheMissingTimestamp_ReturnsFalse()
        {
            var result = CatalogSourceFreshness.IsAlreadySynced(Run(FinishedUTC), [FinishedUTC.AddDays(-2), null]);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsAlreadySynced_CacheWrittenAfterLastRunFinished_ReturnsFalse()
        {
            var result = CatalogSourceFreshness.IsAlreadySynced(Run(FinishedUTC), [FinishedUTC.AddDays(-2), FinishedUTC.AddMinutes(5)]);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsAlreadySynced_LastRunNeverFinished_ReturnsFalse()
        {
            var result = CatalogSourceFreshness.IsAlreadySynced(Run(null), [FinishedUTC.AddDays(-2)]);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsAlreadySynced_NoSuccessfulRun_ReturnsFalse()
        {
            var result = CatalogSourceFreshness.IsAlreadySynced(null, [FinishedUTC.AddDays(-2)]);

            Assert.IsFalse(result);
        }

        private static CatalogSyncRun Run(DateTime? finishedUTC) => new() { FinishedUTC = finishedUTC, Status = CatalogSyncRunStatus.Succeeded };
    }
}
