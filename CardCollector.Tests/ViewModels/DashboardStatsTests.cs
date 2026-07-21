using CardCollector.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CardCollector.Tests.ViewModels
{
    [TestClass]
    public sealed class DashboardStatsTests
    {
        [TestMethod]
        public void PercentCompleted_TotalCardsIsPositive_ReturnsPercentage()
        {
            var stats = new DashboardStats { TotalCards = 200, CompletedCount = 50 };

            Assert.AreEqual(25.0, stats.PercentCompleted);
        }

        [TestMethod]
        public void PercentCompleted_TotalCardsIsZero_ReturnsZeroInsteadOfDividingByZero()
        {
            var stats = new DashboardStats { TotalCards = 0, CompletedCount = 5 };

            Assert.AreEqual(0, stats.PercentCompleted);
        }
        [TestMethod]
        public void PercentIncompleteCopies_TotalCardsIsPositive_ReturnsPercentageOfMissingCopies()
        {
            // Formula: (TotalCardQuantity - CompletedCount * 3) / (TotalCards * 3) * 100.
            var stats = new DashboardStats { TotalCards = 10, CompletedCount = 0, TotalCardQuantity = 20 };

            Assert.AreEqual(20.0 / 30 * 100, stats.PercentIncompleteCopies, 0.0001);
        }

        [TestMethod]
        public void PercentIncompleteCopies_TotalCardsIsZero_ReturnsZero()
        {
            var stats = new DashboardStats { TotalCards = 0, TotalCardQuantity = 10 };

            Assert.AreEqual(0, stats.PercentIncompleteCopies);
        }
        [TestMethod]
        public void PercentOrdered_TotalCardsIsPositive_ReturnsPercentage()
        {
            var stats = new DashboardStats { TotalCards = 10, OrderedCount = 3 };

            Assert.AreEqual(3.0 / 30 * 100, stats.PercentOrdered, 0.0001);
        }

        [TestMethod]
        public void PercentOrdered_TotalCardsIsZero_ReturnsZero()
        {
            var stats = new DashboardStats { TotalCards = 0, OrderedCount = 5 };

            Assert.AreEqual(0, stats.PercentOrdered);
        }
        [TestMethod]
        public void RemainingCount_SubtractsCompletedAndOrderedFromTotal()
        {
            var stats = new DashboardStats { TotalCards = 100, CompletedCount = 40, OrderedCount = 10 };

            Assert.AreEqual(50, stats.RemainingCount);
        }
    }
}
