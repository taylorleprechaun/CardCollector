using CardCollector.Data.Models;
using CardCollector.DTO;
using CardCollector.Repository;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CardCollector.Tests.Services
{
    public partial class CardServiceTests
    {
        [TestMethod]
        public async Task GetDashboardStatsAsync_AggregatesCountsAndValuesAcrossRepositories()
        {
            _cardDataRepositoryMock.Setup(r => r.GetBrowseableCards()).Returns(
            [
                new Card { ID = 1, Name = "Dark Magician", CardSets = [new Set { Code = "LOB-EN001", RarityName = "Ultra Rare" }] },
                new Card { ID = 2, Name = "Ignored Card" }
            ]);
            _ignoredCardRepositoryMock.Setup(r => r.GetIgnoredCardIDsAsync()).ReturnsAsync(new HashSet<int> { 2 });
            _collectionRepositoryMock.Setup(r => r.GetByStatusAsync(CollectionStatus.Owned)).ReturnsAsync(
            [
                new CollectionEntry { ID = 1, CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = "Ultra Rare", Quantity = 3 }
            ]);
            _preferredVersionRepositoryMock.Setup(r => r.GetByImageIDsAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync(new Dictionary<int, PreferredVersion> { [10] = new() { ImageID = 10, SetCode = "LOB-EN001", RarityName = "Ultra Rare" } });
            _collectionRepositoryMock.Setup(r => r.GetByStatusAsync(CollectionStatus.Ordered)).ReturnsAsync(
            [
                new CollectionEntry { ID = 2, CardID = 3 },
                new CollectionEntry { ID = 3, CardID = 4 }
            ]);
            _collectionRepositoryMock.Setup(r => r.GetOwnedStatsAsync()).ReturnsAsync(new OwnedCollectionStats(5, null, 50m));
            _collectionValueRepositoryMock.Setup(r => r.GetLatestSnapshotAsync())
                .ReturnsAsync(new CollectionValueSnapshot { TotalValue = 123.45m, SnapshotDate = "2026-01-01" });
            _collectionRepositoryMock.Setup(r => r.GetOwnedPairsAsync())
                .ReturnsAsync(new HashSet<(int, string)> { (10, "LOB-EN001") });
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CardID = 1, ImageID = 10, SetCode = "LOB-EN001" },
                new PreferredVersion { CardID = 5, ImageID = 99, SetCode = "XYZ-EN001" }
            ]);

            var stats = await _service.GetDashboardStatsAsync();

            Assert.AreEqual(1, stats.TotalCards);
            Assert.AreEqual(1, stats.CompletedCount);
            Assert.AreEqual(0, stats.IncompleteSetCount);
            Assert.AreEqual(2, stats.OrderedCount);
            Assert.AreEqual(5, stats.TotalCardQuantity);
            Assert.AreEqual(50m, stats.TotalSpent);
            Assert.AreEqual(123.45m, stats.CurrentMarketValue);
            Assert.AreEqual("2026-01-01", stats.CurrentMarketValueDate);
            Assert.AreEqual(1, stats.WishlistCount);
        }

        [TestMethod]
        public async Task GetDashboardStatsAsync_NoSnapshotYet_MarketValueIsNull()
        {
            var stats = await _service.GetDashboardStatsAsync();

            Assert.IsNull(stats.CurrentMarketValue);
            Assert.IsNull(stats.CurrentMarketValueDate);
        }
    }
}
