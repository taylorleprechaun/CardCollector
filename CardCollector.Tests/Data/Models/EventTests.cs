using CardCollector.Data.Models;

namespace CardCollector.Tests.Data.Models
{
    [TestClass]
    public sealed class EventTests
    {
        [TestMethod]
        public void FinishText_FinishAndNote_ShowsTheFinish()
        {
            var tournamentEvent = new Event { Finish = 3, FinishNote = "50-70" };

            Assert.AreEqual("3", tournamentEvent.FinishText);
        }

        [TestMethod]
        public void FinishText_NoFinishOrNote_IsNull()
        {
            var tournamentEvent = new Event();

            Assert.IsNull(tournamentEvent.FinishText);
        }

        [TestMethod]
        public void FinishText_OnlyNote_ShowsTheNote()
        {
            var tournamentEvent = new Event { FinishNote = "50-70" };

            Assert.AreEqual("50-70", tournamentEvent.FinishText);
        }
    }
}
