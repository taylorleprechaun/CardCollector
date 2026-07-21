using CardCollector.Data.Models;
using CardCollector.DTO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CardCollector.Tests.Services
{
    public partial class CardServiceTests
    {
        [TestMethod]
        public async Task GetWishlistAsync_MultipleItems_OrdersByCardNameThenSetCode()
        {
            _cardDataRepositoryMock.Setup(r => r.GetCardByID(1)).Returns(new Card { ID = 1, Name = "Zeta Card" });
            _cardDataRepositoryMock.Setup(r => r.GetCardByID(2)).Returns(new Card { ID = 2, Name = "Alpha Card" });
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CardID = 1, ImageID = 10, SetCode = "ZZZ-EN001" },
                new PreferredVersion { CardID = 2, ImageID = 20, SetCode = "AAA-EN001" }
            ]);

            var result = (await _service.GetWishlistAsync()).ToList();

            Assert.AreEqual("Alpha Card", result[0].CardName);
            Assert.AreEqual("Zeta Card", result[1].CardName);
        }

        [TestMethod]
        public async Task GetWishlistAsync_NoPreferredVersions_ReturnsEmpty()
        {
            var result = await _service.GetWishlistAsync();

            Assert.AreEqual(0, result.Count());
        }

        [TestMethod]
        public async Task GetWishlistAsync_OwnedQuantityAtOrAboveThreshold_ExcludesItem()
        {
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = "Ultra Rare" }
            ]);
            _collectionRepositoryMock
                .Setup(r => r.GetOwnedQuantitiesForPreferredVersionsAsync(It.IsAny<IEnumerable<(int, string, string?)>>()))
                .ReturnsAsync(new Dictionary<(int, string), int> { [(10, "LOB-EN001")] = 3 });

            var result = await _service.GetWishlistAsync();

            Assert.AreEqual(0, result.Count());
        }

        [TestMethod]
        public async Task GetWishlistAsync_OwnedQuantityBelowThreshold_IncludesItemWithQuantities()
        {
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = "Ultra Rare" }
            ]);
            _collectionRepositoryMock
                .Setup(r => r.GetOwnedQuantitiesForPreferredVersionsAsync(It.IsAny<IEnumerable<(int, string, string?)>>()))
                .ReturnsAsync(new Dictionary<(int, string), int> { [(10, "LOB-EN001")] = 1 });
            _collectionRepositoryMock.Setup(r => r.GetOrderedQuantitiesAsync())
                .ReturnsAsync(new Dictionary<(int, string, string), int> { [(10, "LOB-EN001", "Ultra Rare")] = 1 });
            _pendingOrderRepositoryMock.Setup(r => r.GetStagedQuantitiesAsync())
                .ReturnsAsync(new Dictionary<(int, string, string), int> { [(10, "LOB-EN001", "Ultra Rare")] = 1 });

            var result = (await _service.GetWishlistAsync()).ToList();

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(1, result[0].QuantityOwned);
            Assert.AreEqual(1, result[0].OrderedQuantity);
            Assert.AreEqual(1, result[0].CartQuantity);
        }
        [TestMethod]
        public async Task GetWishlistDistinctRarityNamesAsync_ReturnsSortedDistinctNonEmptyNames()
        {
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = "Ultra Rare" },
                new PreferredVersion { CardID = 2, ImageID = 20, SetCode = "LOB-EN002", RarityName = "Common" },
                new PreferredVersion { CardID = 3, ImageID = 30, SetCode = "LOB-EN003", RarityName = "Ultra Rare" }
            ]);

            var result = await _service.GetWishlistDistinctRarityNamesAsync();

            CollectionAssert.AreEqual(new[] { "Common", "Ultra Rare" }, result.ToArray());
        }

        [TestMethod]
        public async Task GetWishlistDistinctSetNamesAsync_MapsSetCodeToCanonicalNameWhenKnown()
        {
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CardID = 1, ImageID = 10, SetCode = "LOB-EN001" }
            ]);
            _cardDataRepositoryMock.Setup(r => r.GetSetNamesByCode())
                .Returns(new Dictionary<string, string> { ["LOB-EN001"] = "Legend of Blue Eyes White Dragon" });

            var result = await _service.GetWishlistDistinctSetNamesAsync();

            CollectionAssert.AreEqual(new[] { "Legend of Blue Eyes White Dragon" }, result.ToArray());
        }
    }
}
