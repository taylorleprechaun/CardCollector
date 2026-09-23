using CardCollector.ViewModels;

namespace CardCollector.Tests.ViewModels
{
    [TestClass]
    public sealed class WinLossTieTests
    {
        [TestMethod]
        [DataRow(1, 4, 0, 0.2, DisplayName = "No ties")]
        [DataRow(3, 2, 3, 0.5625, DisplayName = "Ties count as half a win")]
        [DataRow(0, 0, 2, 0.5, DisplayName = "Only ties")]
        [DataRow(5, 0, 0, 1.0, DisplayName = "Only wins")]
        public void WinRate_HasMatches_CountsTiesAsHalfAWin(int wins, int losses, int ties, double expected)
        {
            var record = new WinLossTie(wins, losses, ties);

            Assert.AreEqual(expected, record.WinRate!.Value, 1e-9);
        }

        [TestMethod]
        public void WinRate_NoMatches_IsNull()
        {
            var record = new WinLossTie(0, 0, 0);

            Assert.IsNull(record.WinRate);
        }
    }
}
