using CardCollector.Data.Models;
using CardCollector.DTO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CardCollector.Tests.Services
{
    public partial class CardServiceTests
    {
        [TestMethod]
        public async Task GetGroupedOwnedAsync_CheckedOutLookupMatches_SetsCheckedOutQuantityAndDate()
        {
            SetUpDarkMagicianCard();
            _collectionRepositoryMock.Setup(r => r.GetByStatusAsync(CollectionStatus.Owned)).ReturnsAsync(
            [
                new CollectionEntry { ID = 1, CatalogPrintingID = 101, Quantity = 1 }
            ]);
            var checkedOutDate = new DateTime(2026, 1, 1);
            _checkedOutRepositoryMock.Setup(r => r.GetCheckedOutLookupAsync())
                .ReturnsAsync(new Dictionary<int, (DateTime Date, int Quantity)>
                {
                    [101] = (checkedOutDate, 2)
                });

            var groups = (await _service.GetGroupedOwnedAsync()).ToList();

            Assert.AreEqual(2, groups[0].CheckedOutQuantity);
            Assert.AreEqual(checkedOutDate, groups[0].CheckedOutDate);
            Assert.IsTrue(groups[0].IsCheckedOut);
        }

        [TestMethod]
        public async Task GetGroupedOwnedAsync_EntriesWithPurchasePrice_SumsTotalCost()
        {
            SetUpDarkMagicianCard();
            _collectionRepositoryMock.Setup(r => r.GetByStatusAsync(CollectionStatus.Owned)).ReturnsAsync(
            [
                new CollectionEntry { ID = 1, CatalogPrintingID = 101, Quantity = 2, PurchasePrice = 5.00m },
                new CollectionEntry { ID = 2, CatalogPrintingID = 101, Quantity = 1, PurchasePrice = null }
            ]);

            var groups = (await _service.GetGroupedOwnedAsync()).ToList();

            Assert.AreEqual(10.00m, groups[0].TotalCost);
            Assert.AreEqual(3, groups[0].TotalQuantity);
        }

        [TestMethod]
        public async Task GetGroupedOwnedAsync_MultipleCards_OrdersByCardNameThenSetCode()
        {
            SetUpCard(1, "Zeta Card", new Set { Code = "ZZZ-EN001", PrintingID = 101 });
            SetUpCard(2, "Alpha Card", new Set { Code = "AAA-EN001", PrintingID = 201 });
            _collectionRepositoryMock.Setup(r => r.GetByStatusAsync(CollectionStatus.Owned)).ReturnsAsync(
            [
                new CollectionEntry { ID = 1, CatalogPrintingID = 101, Quantity = 1 },
                new CollectionEntry { ID = 2, CatalogPrintingID = 201, Quantity = 1 }
            ]);

            var groups = (await _service.GetGroupedOwnedAsync()).ToList();

            Assert.AreEqual("Alpha Card", groups[0].CardName);
            Assert.AreEqual("Zeta Card", groups[1].CardName);
        }

        [TestMethod]
        public async Task GetGroupedOwnedAsync_NoEntriesHavePurchasePrice_TotalCostIsNull()
        {
            SetUpDarkMagicianCard();
            _collectionRepositoryMock.Setup(r => r.GetByStatusAsync(CollectionStatus.Owned)).ReturnsAsync(
            [
                new CollectionEntry { ID = 1, CatalogPrintingID = 101, Quantity = 1, PurchasePrice = null }
            ]);

            var groups = (await _service.GetGroupedOwnedAsync()).ToList();

            Assert.IsNull(groups[0].TotalCost);
        }

        [TestMethod]
        public async Task GetGroupedOwnedAsync_NonPreferredGroupWhosePreferredIsComplete_RollsUpToOwned()
        {
            SetUpDarkMagicianCard();
            _collectionRepositoryMock.Setup(r => r.GetByStatusAsync(CollectionStatus.Owned)).ReturnsAsync(
            [
                new CollectionEntry { ID = 1, CatalogPrintingID = 101, Quantity = 3 },
                new CollectionEntry { ID = 2, CatalogPrintingID = 102, Quantity = 1 }
            ]);
            _preferredVersionRepositoryMock.Setup(r => r.GetByCardIDsAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync(new Dictionary<int, IReadOnlyList<PreferredVersion>> { [1] = [new() { CatalogPrintingID = 101 }] });

            var groups = (await _service.GetGroupedOwnedAsync()).ToList();

            var nonPreferredGroup = groups.Single(g => g.SetCode == "LOB-EN002");
            Assert.IsFalse(nonPreferredGroup.IsPreferredVersion);
            Assert.AreEqual(CollectionCompletionStatus.Owned, nonPreferredGroup.CompletionStatus);
        }

        [TestMethod]
        public async Task GetGroupedOwnedAsync_NonPreferredGroupWhosePreferredIsIncomplete_ReturnsPlaceholder()
        {
            SetUpDarkMagicianCard();
            _collectionRepositoryMock.Setup(r => r.GetByStatusAsync(CollectionStatus.Owned)).ReturnsAsync(
            [
                new CollectionEntry { ID = 1, CatalogPrintingID = 101, Quantity = 1 },
                new CollectionEntry { ID = 2, CatalogPrintingID = 102, Quantity = 1 }
            ]);
            _preferredVersionRepositoryMock.Setup(r => r.GetByCardIDsAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync(new Dictionary<int, IReadOnlyList<PreferredVersion>> { [1] = [new() { CatalogPrintingID = 101 }] });

            var groups = (await _service.GetGroupedOwnedAsync()).ToList();

            var nonPreferredGroup = groups.Single(g => g.SetCode == "LOB-EN002");
            Assert.AreEqual(CollectionCompletionStatus.Placeholder, nonPreferredGroup.CompletionStatus);
        }

        [TestMethod]
        public async Task GetGroupedOwnedAsync_PreferredVersionAtThreshold_ReturnsComplete()
        {
            SetUpDarkMagicianCard();
            _collectionRepositoryMock.Setup(r => r.GetByStatusAsync(CollectionStatus.Owned)).ReturnsAsync(
            [
                new CollectionEntry { ID = 1, CatalogPrintingID = 101, Quantity = 3 }
            ]);
            _preferredVersionRepositoryMock.Setup(r => r.GetByCardIDsAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync(new Dictionary<int, IReadOnlyList<PreferredVersion>> { [1] = [new() { CatalogPrintingID = 101 }] });

            var groups = (await _service.GetGroupedOwnedAsync()).ToList();

            Assert.AreEqual(1, groups.Count);
            Assert.AreEqual(CollectionCompletionStatus.Complete, groups[0].CompletionStatus);
        }

        [TestMethod]
        public async Task GetGroupedOwnedAsync_PreferredVersionBelowThreshold_ReturnsIncomplete()
        {
            SetUpDarkMagicianCard();
            _collectionRepositoryMock.Setup(r => r.GetByStatusAsync(CollectionStatus.Owned)).ReturnsAsync(
            [
                new CollectionEntry { ID = 1, CatalogPrintingID = 101, Quantity = 1 }
            ]);
            _preferredVersionRepositoryMock.Setup(r => r.GetByCardIDsAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync(new Dictionary<int, IReadOnlyList<PreferredVersion>> { [1] = [new() { CatalogPrintingID = 101 }] });

            var groups = (await _service.GetGroupedOwnedAsync()).ToList();

            Assert.AreEqual(CollectionCompletionStatus.Incomplete, groups[0].CompletionStatus);
        }

        [TestMethod]
        public async Task GetGroupedOwnedAsync_TwoTrackedPrintingsForSameCard_EachIndependentlyReflectsOwnTarget()
        {
            SetUpDarkMagicianCard();
            _collectionRepositoryMock.Setup(r => r.GetByStatusAsync(CollectionStatus.Owned)).ReturnsAsync(
            [
                new CollectionEntry { ID = 1, CatalogPrintingID = 101, Quantity = 1 },
                new CollectionEntry { ID = 2, CatalogPrintingID = 102, Quantity = 1 }
            ]);
            _preferredVersionRepositoryMock.Setup(r => r.GetByCardIDsAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync(new Dictionary<int, IReadOnlyList<PreferredVersion>>
                {
                    [1] =
                    [
                        new() { CatalogPrintingID = 101, DesiredQuantity = 3 },
                        new() { CatalogPrintingID = 102, DesiredQuantity = 1 }
                    ]
                });

            var groups = (await _service.GetGroupedOwnedAsync()).ToList();

            var ultraRareGroup = groups.Single(g => g.SetCode == "LOB-EN001");
            var secretRareGroup = groups.Single(g => g.SetCode == "LOB-EN002");
            Assert.IsTrue(ultraRareGroup.IsPreferredVersion);
            Assert.AreEqual(CollectionCompletionStatus.Incomplete, ultraRareGroup.CompletionStatus);
            Assert.IsTrue(secretRareGroup.IsPreferredVersion);
            Assert.AreEqual(CollectionCompletionStatus.Complete, secretRareGroup.CompletionStatus);
        }

        [TestMethod]
        public async Task GetGroupedOwnedAsync_TwoVariantsUnderOneSetCode_AreSeparateGroups()
        {
            SetUpCard(1, "Synthetic Dragon",
                new Set { Code = "RA05-EN146", PrintingID = 101, RarityName = "Ultra Rare" },
                new Set { Code = "RA05-EN146", PrintingID = 102, PrintVariant = "Extended Art", RarityName = "Ultra Rare" });
            _collectionRepositoryMock.Setup(r => r.GetByStatusAsync(CollectionStatus.Owned)).ReturnsAsync(
            [
                new CollectionEntry { ID = 1, CatalogPrintingID = 101, Quantity = 1 },
                new CollectionEntry { ID = 2, CatalogPrintingID = 102, Quantity = 2 }
            ]);

            var groups = (await _service.GetGroupedOwnedAsync()).ToList();

            CollectionAssert.AreEquivalent(new[] { 101, 102 }, groups.Select(g => g.PrintingID).ToArray());
        }

        private void SetUpDarkMagicianCard() =>
            SetUpCard(1, "Dark Magician",
                new Set { Code = "LOB-EN001", PrintingID = 101, RarityName = "Ultra Rare" },
                new Set { Code = "LOB-EN002", PrintingID = 102, RarityName = "Secret Rare" });
    }
}
