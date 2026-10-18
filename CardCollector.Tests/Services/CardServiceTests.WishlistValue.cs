using CardCollector.Data.Models;
using Moq;

namespace CardCollector.Tests.Services
{
    public partial class CardServiceTests
    {
        [TestMethod]
        public async Task CalculateWishlistRemainingValueAsync_LivePriceUnavailable_ContributesZeroToTotal()
        {
            SetUpPrinting(101, "LOB-EN001", "Ultra Rare");
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CatalogPrintingID = 101 }
            ]);
            _pricingServiceMock
                .Setup(p => p.GetPrintingPriceAsync(101, null))
                .ReturnsAsync((decimal?)null);

            var (totalValue, countRemaining) = await _service.CalculateWishlistRemainingValueAsync();

            Assert.AreEqual(0m, totalValue);
            Assert.AreEqual(3, countRemaining);
        }

        [TestMethod]
        public async Task CalculateWishlistRemainingValueAsync_NoSnapshotYet_FetchesLivePriceAndPersistsSnapshot()
        {
            SetUpPrinting(101, "LOB-EN001", "Ultra Rare");
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CatalogPrintingID = 101 }
            ]);
            _pricingServiceMock
                .Setup(p => p.GetPrintingPriceAsync(101, null))
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
            SetUpPrinting(101, "LOB-EN001", "Ultra Rare");
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CatalogPrintingID = 101 }
            ]);
            _collectionRepositoryMock.Setup(r => r.GetOwnedQuantitiesByPrintingAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync(new Dictionary<int, int> { [101] = 1 });
            _collectionRepositoryMock.Setup(r => r.GetOrderedQuantitiesAsync()).ReturnsAsync(new Dictionary<int, int> { [101] = 2 });
            _pricingServiceMock
                .Setup(p => p.GetPrintingPriceAsync(101, null))
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
