using CardCollector.Extensions;

namespace CardCollector.Tests.Extensions
{
    [TestClass]
    public sealed class DateTimeExtensionsTests
    {
        private static readonly DateTime NowUTC = new(2026, 10, 18, 9, 30, 0, DateTimeKind.Utc);

        [TestMethod]
        public void ToRelativeDate_FutureDate_ReturnsTheDate()
        {
            var tomorrow = new DateTime(2026, 10, 19, 8, 0, 0, DateTimeKind.Utc);

            var result = tomorrow.ToRelativeDate(NowUTC);

            Assert.AreEqual("Oct 19, 2026", result);
        }

        [TestMethod]
        [DataRow("2026-10-18T23:59:00", "today", DisplayName = "Later the same day")]
        [DataRow("2026-10-18T00:00:00", "today", DisplayName = "Start of today")]
        [DataRow("2026-10-17T23:00:00", "yesterday", DisplayName = "Late yesterday")]
        [DataRow("2026-10-15T12:00:00", "3 days ago", DisplayName = "Three days ago")]
        [DataRow("2026-10-12T12:00:00", "6 days ago", DisplayName = "Six days ago")]
        [DataRow("2026-10-11T12:00:00", "Oct 11, 2026", DisplayName = "A week ago")]
        [DataRow("2025-01-02T12:00:00", "Jan 2, 2025", DisplayName = "Last year")]
        public void ToRelativeDate_PastDate_ReturnsExpectedText(string utc, string expected)
        {
            var date = DateTime.SpecifyKind(DateTime.Parse(utc, System.Globalization.CultureInfo.InvariantCulture), DateTimeKind.Utc);

            var result = date.ToRelativeDate(NowUTC);

            Assert.AreEqual(expected, result);
        }
    }
}
