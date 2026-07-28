using CardCollector.Data.Models;
using CardCollector.DTO;
using Moq;

namespace CardCollector.Tests.Services
{
    public partial class CardServiceTests
    {
        [TestMethod]
        public async Task CalculateWishlistRemainingValueAsync_LivePriceUnavailable_ContributesZeroToTotal()
        {
            _cardDataRepositoryMock.Setup(r => r.GetCardByID(1)).Returns(new Card { ID = 1, Name = "Dark Magician" });
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = "Ultra Rare" }
            ]);
            _pricingServiceMock
                .Setup(p => p.GetPrintingPriceAsync(1, "LOB-EN001", "Ultra Rare", null))
                .ReturnsAsync((decimal?)null);

            var (totalValue, countRemaining) = await _service.CalculateWishlistRemainingValueAsync();

            Assert.AreEqual(0m, totalValue);
            Assert.AreEqual(3, countRemaining);
        }

        [TestMethod]
        public async Task CalculateWishlistRemainingValueAsync_NoSnapshotYet_FetchesLivePriceAndPersistsSnapshot()
        {
            _cardDataRepositoryMock.Setup(r => r.GetCardByID(1)).Returns(new Card { ID = 1, Name = "Dark Magician" });
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = "Ultra Rare" }
            ]);
            _pricingServiceMock
                .Setup(p => p.GetPrintingPriceAsync(1, "LOB-EN001", "Ultra Rare", null))
                .ReturnsAsync(10m);

            var (totalValue, countRemaining) = await _service.CalculateWishlistRemainingValueAsync();

            Assert.AreEqual(30m, totalValue);
            Assert.AreEqual(3, countRemaining);
            _wishlistValueRepositoryMock.Verify(
                r => r.UpsertSnapshotAsync(It.Is<WishlistValueSnapshot>(s => s.TotalValue == 30m && s.RemainingCount == 3 && s.SnapshotDate == Today)),
                Times.Once);
        }

        [TestMethod]
        public async Task CalculateWishlistRemainingValueAsync_QuantityNeededIsZero_ContributesZeroToTotalAndCount()
        {
            _cardDataRepositoryMock.Setup(r => r.GetCardByID(1)).Returns(new Card { ID = 1, Name = "Dark Magician" });
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = "Ultra Rare" }
            ]);
            _collectionRepositoryMock
                .Setup(r => r.GetOwnedQuantitiesForPreferredVersionsAsync(It.IsAny<IEnumerable<(int, string, string?)>>()))
                .ReturnsAsync(new Dictionary<(int, string), int> { [(10, "LOB-EN001")] = 1 });
            _collectionRepositoryMock.Setup(r => r.GetOrderedQuantitiesAsync())
                .ReturnsAsync(new Dictionary<(int, string, string), int> { [(10, "LOB-EN001", "Ultra Rare")] = 2 });
            _pricingServiceMock
                .Setup(p => p.GetPrintingPriceAsync(1, "LOB-EN001", "Ultra Rare", null))
                .ReturnsAsync(10m);

            var (totalValue, countRemaining) = await _service.CalculateWishlistRemainingValueAsync();

            Assert.AreEqual(0m, totalValue);
            Assert.AreEqual(0, countRemaining);
        }

        [TestMethod]
        public async Task CalculateWishlistRemainingValueAsync_SnapshotFromToday_ReturnsCachedWithoutRecomputation()
        {
            _wishlistValueRepositoryMock.Setup(r => r.GetLatestSnapshotAsync())
                .ReturnsAsync(new WishlistValueSnapshot { SnapshotDate = Today, TotalValue = 50m, RemainingCount = 7 });

            var (totalValue, countRemaining) = await _service.CalculateWishlistRemainingValueAsync();

            Assert.AreEqual(50m, totalValue);
            Assert.AreEqual(7, countRemaining);
            _preferredVersionRepositoryMock.Verify(r => r.GetAllAsync(), Times.Never);
            _wishlistValueRepositoryMock.Verify(r => r.UpsertSnapshotAsync(It.IsAny<WishlistValueSnapshot>()), Times.Never);
        }
    }
}
