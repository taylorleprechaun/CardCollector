using CardCollector.Services;
using CardCollector.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CardCollector.Tests.ViewModels
{
    [TestClass]
    public sealed class PurchasePriorityCandidateViewModelTests
    {
        private static readonly PurchasePriorityCandidate Candidate = new()
        {
            CardID = 1,
            CardName = "Dark Magician",
            DebutDate = "2002-03-08",
            FoilCount = 2,
            PrintingDate = "2002-03-08"
        };

        [TestMethod]
        public void From_MapsCandidateFieldsOntoNewInstance()
        {
            var printing = new CardPrinting { CardID = 1, CardName = "Dark Magician" };

            var result = PurchasePriorityCandidateViewModel.From(printing, Candidate, hasAmbiguousSetCode: true);

            Assert.AreEqual(Candidate.DebutDate, result.DebutDate);
            Assert.AreEqual(Candidate.FoilCount, result.FoilCount);
            Assert.AreEqual(Candidate.PrintingDate, result.PrintingDate);
            Assert.IsTrue(result.HasAmbiguousSetCode);
        }

        [TestMethod]
        public void IsInCart_CartQuantityIsPositive_ReturnsTrue()
        {
            var candidate = PurchasePriorityCandidateViewModel.From(new CardPrinting(), Candidate, cartQuantity: 1);

            Assert.IsTrue(candidate.IsInCart);
        }

        [TestMethod]
        public void IsOrdered_OrderedQuantityIsPositive_ReturnsTrue()
        {
            var candidate = PurchasePriorityCandidateViewModel.From(new CardPrinting(), Candidate, orderedQuantity: 1);

            Assert.IsTrue(candidate.IsOrdered);
        }

        [TestMethod]
        public void LineTotal_MultipliesPriceByQuantityNeeded()
        {
            var printing = new CardPrinting { Price = 5.00m };

            var candidate = PurchasePriorityCandidateViewModel.From(printing, Candidate, quantityOwned: 1);

            Assert.AreEqual(10.00m, candidate.LineTotal);
        }

        [TestMethod]
        public void LineTotal_PriceIsNull_TreatsPriceAsZero()
        {
            var printing = new CardPrinting { Price = null };

            var candidate = PurchasePriorityCandidateViewModel.From(printing, Candidate, quantityOwned: 0);

            Assert.AreEqual(0m, candidate.LineTotal);
        }

        [TestMethod]
        [DataRow(0, 0, 0, 3, DisplayName = "Nothing owned needs full threshold")]
        [DataRow(1, 1, 0, 1, DisplayName = "Partial progress reduces quantity needed")]
        [DataRow(3, 0, 0, 0, DisplayName = "Already owns enough copies")]
        [DataRow(2, 2, 2, 0, DisplayName = "Overshooting threshold clamps to zero instead of going negative")]
        public void QuantityNeeded_ClampsAtZeroAndNeverNegative(int quantityOwned, int orderedQuantity, int cartQuantity, int expected)
        {
            var candidate = PurchasePriorityCandidateViewModel.From(
                new CardPrinting(), Candidate, quantityOwned, cartQuantity: cartQuantity, orderedQuantity: orderedQuantity);

            Assert.AreEqual(expected, candidate.QuantityNeeded);
        }
    }
}
