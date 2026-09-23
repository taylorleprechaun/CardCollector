using CardCollector.ViewModels;

namespace CardCollector.Tests.ViewModels
{
    [TestClass]
    public sealed class AnalyticsDisplayTests
    {
        [TestMethod]
        [DataRow(0.6409952606635071, "64.1%", DisplayName = "Rounds to one decimal")]
        [DataRow(0.0, "0.0%", DisplayName = "Zero")]
        [DataRow(1.0, "100.0%", DisplayName = "One hundred percent")]
        public void Percent_HasValue_ShowsPercentageWithOneDecimal(double ratio, string expected)
        {
            Assert.AreEqual(expected, AnalyticsDisplay.Percent(ratio));
        }

        [TestMethod]
        public void Percent_Missing_ShowsDash()
        {
            Assert.AreEqual(AnalyticsDisplay.MISSING, AnalyticsDisplay.Percent(null));
        }

        [TestMethod]
        public void SortValue_HasValue_UsesInvariantRoundTripFormat()
        {
            Assert.AreEqual("0.5625", AnalyticsDisplay.SortValue(0.5625));
        }

        [TestMethod]
        public void SortValue_Missing_SortsBelowEveryRealValue()
        {
            Assert.AreEqual("-1", AnalyticsDisplay.SortValue(null));
        }
    }
}
