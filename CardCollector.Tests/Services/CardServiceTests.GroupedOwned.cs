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
                new CollectionEntry { ID = 1, CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = "Ultra Rare", Quantity = 1 }
            ]);
            var checkedOutDate = new DateTime(2026, 1, 1);
            _checkedOutRepositoryMock.Setup(r => r.GetCheckedOutLookupAsync())
                .ReturnsAsync(new Dictionary<(int ImageID, string SetCode, string RarityName), (DateTime Date, int Quantity)>
                {
                    [(10, "LOB-EN001", "Ultra Rare")] = (checkedOutDate, 2)
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
                new CollectionEntry { ID = 1, CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = "Ultra Rare", Quantity = 2, PurchasePrice = 5.00m },
                new CollectionEntry { ID = 2, CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = "Ultra Rare", Quantity = 1, PurchasePrice = null }
            ]);

            var groups = (await _service.GetGroupedOwnedAsync()).ToList();

            Assert.AreEqual(10.00m, groups[0].TotalCost);
            Assert.AreEqual(3, groups[0].TotalQuantity);
        }

        [TestMethod]
        public async Task GetGroupedOwnedAsync_MultipleCards_OrdersByCardNameThenSetCode()
        {
            _cardDataRepositoryMock.Setup(r => r.GetCardByID(1)).Returns(new Card { ID = 1, Name = "Zeta Card" });
            _cardDataRepositoryMock.Setup(r => r.GetCardByID(2)).Returns(new Card { ID = 2, Name = "Alpha Card" });
            _collectionRepositoryMock.Setup(r => r.GetByStatusAsync(CollectionStatus.Owned)).ReturnsAsync(
            [
                new CollectionEntry { ID = 1, CardID = 1, ImageID = 10, SetCode = "ZZZ-EN001", Quantity = 1 },
                new CollectionEntry { ID = 2, CardID = 2, ImageID = 20, SetCode = "AAA-EN001", Quantity = 1 }
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
                new CollectionEntry { ID = 1, CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = "Ultra Rare", Quantity = 1, PurchasePrice = null }
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
                new CollectionEntry { ID = 1, CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = "Ultra Rare", Quantity = 3 },
                new CollectionEntry { ID = 2, CardID = 1, ImageID = 10, SetCode = "LOB-EN002", RarityName = "Secret Rare", Quantity = 1 }
            ]);
            _preferredVersionRepositoryMock.Setup(r => r.GetByImageIDsAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync(new Dictionary<int, PreferredVersion> { [10] = new() { ImageID = 10, SetCode = "LOB-EN001", RarityName = "Ultra Rare" } });

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
                new CollectionEntry { ID = 1, CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = "Ultra Rare", Quantity = 1 },
                new CollectionEntry { ID = 2, CardID = 1, ImageID = 10, SetCode = "LOB-EN002", RarityName = "Secret Rare", Quantity = 1 }
            ]);
            _preferredVersionRepositoryMock.Setup(r => r.GetByImageIDsAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync(new Dictionary<int, PreferredVersion> { [10] = new() { ImageID = 10, SetCode = "LOB-EN001", RarityName = "Ultra Rare" } });

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
                new CollectionEntry { ID = 1, CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = "Ultra Rare", Quantity = 3 }
            ]);
            _preferredVersionRepositoryMock.Setup(r => r.GetByImageIDsAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync(new Dictionary<int, PreferredVersion> { [10] = new() { ImageID = 10, SetCode = "LOB-EN001", RarityName = "Ultra Rare" } });

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
                new CollectionEntry { ID = 1, CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = "Ultra Rare", Quantity = 1 }
            ]);
            _preferredVersionRepositoryMock.Setup(r => r.GetByImageIDsAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync(new Dictionary<int, PreferredVersion> { [10] = new() { ImageID = 10, SetCode = "LOB-EN001", RarityName = "Ultra Rare" } });

            var groups = (await _service.GetGroupedOwnedAsync()).ToList();

            Assert.AreEqual(CollectionCompletionStatus.Incomplete, groups[0].CompletionStatus);
        }

        private void SetUpDarkMagicianCard() =>
                                                                            _cardDataRepositoryMock.Setup(r => r.GetCardByID(1)).Returns(new Card
            {
                ID = 1,
                Name = "Dark Magician",
                CardSets =
                [
                    new Set { Code = "LOB-EN001", RarityName = "Ultra Rare" },
                    new Set { Code = "LOB-EN002", RarityName = "Secret Rare" }
                ]
            });
    }
}
