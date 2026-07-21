using CardCollector.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CardCollector.Tests.Services
{
    [TestClass]
    public sealed class PriceRefreshBackgroundServiceTests
    {
        [TestMethod]
        public void GetDelayUntilNextMidnightEastern_AcrossDstFallBackTransition_ReturnsPositiveDelay()
        {
            // 2026-11-01 is the Eastern "fall back" DST transition (2:00 AM EDT -> 1:00 AM EST).
            // Midnight itself isn't inside the ambiguous 1-2 AM window, so the delay should still be positive.
            var nowUtc = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

            var result = PriceRefreshBackgroundService.GetDelayUntilNextMidnightEastern(nowUtc);

            Assert.IsTrue(result > TimeSpan.Zero);
            Assert.IsTrue(result <= TimeSpan.FromHours(25));
        }

        [TestMethod]
        public void GetDelayUntilNextMidnightEastern_JustAfterEasternMidnight_ReturnsDelayCloseToTwentyFourHours()
        {
            // 2026-07-16 04:00:01 UTC = 2026-07-16 00:00:01 Eastern (EDT, UTC-4).
            var nowUtc = new DateTime(2026, 7, 16, 4, 0, 1, DateTimeKind.Utc);

            var result = PriceRefreshBackgroundService.GetDelayUntilNextMidnightEastern(nowUtc);

            Assert.AreEqual(TimeSpan.FromHours(24) - TimeSpan.FromSeconds(1), result);
        }

        [TestMethod]
        public void GetDelayUntilNextMidnightEastern_MidDayInstant_ReturnsDelayUntilNextEasternMidnight()
        {
            // 2026-07-15 12:00 UTC = 08:00 Eastern (EDT, UTC-4) on the same day.
            var nowUtc = new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc);

            var result = PriceRefreshBackgroundService.GetDelayUntilNextMidnightEastern(nowUtc);

            // Next Eastern midnight is 2026-07-16 00:00 Eastern = 2026-07-16 04:00 UTC.
            Assert.AreEqual(TimeSpan.FromHours(16), result);
        }

        [TestMethod]
        public void GetDelayUntilNextMidnightEastern_SecondsBeforeEasternMidnight_ReturnsShortDelay()
        {
            // 2026-07-16 03:59:50 UTC = 2026-07-15 23:59:50 Eastern (EDT, UTC-4).
            var nowUtc = new DateTime(2026, 7, 16, 3, 59, 50, DateTimeKind.Utc);

            var result = PriceRefreshBackgroundService.GetDelayUntilNextMidnightEastern(nowUtc);

            Assert.AreEqual(TimeSpan.FromSeconds(10), result);
        }
    }
}
