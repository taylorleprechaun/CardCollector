using CardCollector.Data.Models;
using CardCollector.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CardCollector.Tests.ViewModels
{
    [TestClass]
    public sealed class CollectionGroupViewModelTests
    {
        [TestMethod]
        public void CompletionStatus_NotPreferredVersionAndPreferredIsComplete_ReturnsOwned()
        {
            var group = new CollectionGroupViewModel
            {
                IsPreferredVersion = false,
                PreferredVersionIsComplete = true
            };

            Assert.AreEqual(CollectionCompletionStatus.Owned, group.CompletionStatus);
        }

        [TestMethod]
        public void CompletionStatus_NotPreferredVersionAndPreferredIsIncomplete_ReturnsPlaceholder()
        {
            var group = new CollectionGroupViewModel
            {
                IsPreferredVersion = false,
                PreferredVersionIsComplete = false
            };

            Assert.AreEqual(CollectionCompletionStatus.Placeholder, group.CompletionStatus);
        }

        [TestMethod]
        public void CompletionStatus_PreferredVersionWithQuantityAtThreshold_ReturnsComplete()
        {
            var group = new CollectionGroupViewModel
            {
                IsPreferredVersion = true,
                Entries = [MakeEntry(2), MakeEntry(1)]
            };

            Assert.AreEqual(CollectionCompletionStatus.Complete, group.CompletionStatus);
        }

        [TestMethod]
        public void CompletionStatus_PreferredVersionWithQuantityBelowThreshold_ReturnsIncomplete()
        {
            var group = new CollectionGroupViewModel
            {
                IsPreferredVersion = true,
                Entries = [MakeEntry(1)]
            };

            Assert.AreEqual(CollectionCompletionStatus.Incomplete, group.CompletionStatus);
        }

        [TestMethod]
        public void From_MapsPrintingAndEntryFieldsOntoNewInstance()
        {
            var printing = new CardPrinting { CardID = 5, CardName = "Dark Magician", SetCode = "LOB-EN005" };
            var entries = new List<OrderEntryViewModel> { MakeEntry(3) };

            var result = CollectionGroupViewModel.From(
                printing, entries, isPreferredVersion: true, preferredVersionIsComplete: false,
                totalCost: 12.50m, totalQuantity: 3, checkedOutQuantity: 1, checkedOutDate: new DateTime(2026, 1, 1));

            Assert.AreEqual(5, result.CardID);
            Assert.AreEqual("Dark Magician", result.CardName);
            Assert.AreEqual("LOB-EN005", result.SetCode);
            Assert.AreSame(entries, result.Entries);
            Assert.IsTrue(result.IsPreferredVersion);
            Assert.AreEqual(12.50m, result.TotalCost);
            Assert.AreEqual(3, result.TotalQuantity);
            Assert.AreEqual(1, result.CheckedOutQuantity);
            Assert.AreEqual(new DateTime(2026, 1, 1), result.CheckedOutDate);
        }

        [TestMethod]
        public void IsCheckedOut_CheckedOutQuantityIsPositive_ReturnsTrue()
        {
            var group = new CollectionGroupViewModel { CheckedOutQuantity = 1 };

            Assert.IsTrue(group.IsCheckedOut);
        }

        [TestMethod]
        public void IsCheckedOut_CheckedOutQuantityIsZero_ReturnsFalse()
        {
            var group = new CollectionGroupViewModel { CheckedOutQuantity = 0 };

            Assert.IsFalse(group.IsCheckedOut);
        }
        private static OrderEntryViewModel MakeEntry(int quantity) => new() { Quantity = quantity };
    }
}
