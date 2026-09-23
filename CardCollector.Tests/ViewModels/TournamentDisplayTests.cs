using CardCollector.Data.Models;
using CardCollector.ViewModels;

namespace CardCollector.Tests.ViewModels
{
    [TestClass]
    public sealed class TournamentDisplayTests
    {
        [TestMethod]
        public void Date_AnyDate_ShowsShortMonthDayAndYear()
        {
            Assert.AreEqual("Jul 8, 2021", TournamentDisplay.Date(new DateOnly(2021, 7, 8)));
        }

        [TestMethod]
        public void DateRange_EndedFormat_ShowsBothDates()
        {
            var format = new Format { StartDate = new DateOnly(2021, 7, 8), EndDate = new DateOnly(2021, 8, 11) };

            Assert.AreEqual("Jul 8, 2021 – Aug 11, 2021", TournamentDisplay.DateRange(format));
        }

        [TestMethod]
        public void DateRange_OngoingFormat_EndsWithOngoing()
        {
            var format = new Format { StartDate = new DateOnly(2025, 1, 2) };

            Assert.AreEqual("Jan 2, 2025 – Ongoing", TournamentDisplay.DateRange(format));
        }

        [TestMethod]
        public void DateRange_NullFormat_ThrowsArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => TournamentDisplay.DateRange(null!));
        }

        [TestMethod]
        public void IsoDate_HasDate_ShowsYearMonthDay()
        {
            Assert.AreEqual("2024-04-05", TournamentDisplay.IsoDate(new DateOnly(2024, 4, 5)));
        }

        [TestMethod]
        public void IsoDate_NoDate_ReturnsNull()
        {
            Assert.IsNull(TournamentDisplay.IsoDate(null));
        }

        [TestMethod]
        [DataRow(0.6409952606635071, "64.1%", DisplayName = "Rounds to one decimal")]
        [DataRow(0.0, "0.0%", DisplayName = "Zero")]
        [DataRow(1.0, "100.0%", DisplayName = "One hundred percent")]
        public void Percent_HasValue_ShowsPercentageWithOneDecimal(double ratio, string expected)
        {
            Assert.AreEqual(expected, TournamentDisplay.Percent(ratio));
        }

        [TestMethod]
        public void Percent_Missing_ShowsDash()
        {
            Assert.AreEqual(TournamentDisplay.MISSING, TournamentDisplay.Percent(null));
        }

        [TestMethod]
        public void SortValue_HasValue_UsesInvariantRoundTripFormat()
        {
            Assert.AreEqual("0.5625", TournamentDisplay.SortValue(0.5625));
        }

        [TestMethod]
        public void SortValue_Missing_SortsBelowEveryRealValue()
        {
            Assert.AreEqual("-1", TournamentDisplay.SortValue(null));
        }
    }
}
