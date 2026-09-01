using CardCollector.Data.Models;
using CardCollector.DTO;
using Moq;

namespace CardCollector.Tests.Services
{
    public partial class CardServiceTests
    {
        [TestMethod]
        public async Task GetWishlistAsync_CalledMultipleTimes_OnlyQueriesRepositoriesOnce()
        {
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = "Ultra Rare" }
            ]);
            _collectionRepositoryMock
                .Setup(r => r.GetOwnedQuantitiesForPreferredVersionsAsync(It.IsAny<IEnumerable<(int, string, string?)>>()))
                .ReturnsAsync(new Dictionary<(int, string), int>());
            _collectionRepositoryMock.Setup(r => r.GetOrderedQuantitiesAsync())
                .ReturnsAsync(new Dictionary<(int, string, string), int>());
            _pendingOrderRepositoryMock.Setup(r => r.GetStagedQuantitiesAsync())
                .ReturnsAsync(new Dictionary<(int, string, string), int>());

            await _service.GetWishlistAsync();
            await _service.GetWishlistAsync();

            _preferredVersionRepositoryMock.Verify(r => r.GetAllAsync(), Times.Once());
            _collectionRepositoryMock.Verify(
                r => r.GetOwnedQuantitiesForPreferredVersionsAsync(It.IsAny<IEnumerable<(int, string, string?)>>()), Times.Once());
            _collectionRepositoryMock.Verify(r => r.GetOrderedQuantitiesAsync(), Times.Once());
            _pendingOrderRepositoryMock.Verify(r => r.GetStagedQuantitiesAsync(), Times.Once());
        }

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
        public async Task GetWishlistAsync_NoPreferredVersions_CachesEmptyResult()
        {
            await _service.GetWishlistAsync();
            await _service.GetWishlistAsync();

            _preferredVersionRepositoryMock.Verify(r => r.GetAllAsync(), Times.Once());
        }

        [TestMethod]
        public async Task GetWishlistAsync_NoPreferredVersions_ReturnsEmpty()
        {
            var result = await _service.GetWishlistAsync();

            Assert.AreEqual(0, result.Count());
        }

        [TestMethod]
        public async Task GetWishlistAsync_OwnedQuantityAtCustomDesiredQuantity_ExcludesItem()
        {
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = "Ultra Rare", DesiredQuantity = 1 }
            ]);
            _collectionRepositoryMock
                .Setup(r => r.GetOwnedQuantitiesForPreferredVersionsAsync(It.IsAny<IEnumerable<(int, string, string?)>>()))
                .ReturnsAsync(new Dictionary<(int, string), int> { [(10, "LOB-EN001")] = 1 });

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
        public async Task GetWishlistAsync_PreferredVersionIsCommonButCatalogRarityIsShortPrint_ResolvesPriceAndSetName()
        {
            _cardDataRepositoryMock.Setup(r => r.GetCardByID(1)).Returns(new Card
            {
                ID = 1,
                Name = "Dark Magician",
                CardSets = [new Set { Code = "LOB-EN001", RarityName = "Short Print", Price = 5m, Name = "Legend of Blue Eyes White Dragon" }]
            });
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = "Common" }
            ]);

            var result = (await _service.GetWishlistAsync()).ToList();

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("Common", result[0].RarityName);
            Assert.AreEqual("(C)", result[0].RarityCode);
            Assert.AreEqual("Legend of Blue Eyes White Dragon", result[0].SetName);
            Assert.AreEqual(5m, result[0].Price);
        }

        [TestMethod]
        public async Task GetWishlistAsync_TwoTrackedPrintingsForSameCard_BothAppearIndependently()
        {
            _cardDataRepositoryMock.Setup(r => r.GetCardByID(1)).Returns(new Card { ID = 1, Name = "A Bao A Qu, the Lightless Shadow" });
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CardID = 1, ImageID = 10, SetCode = "SUDA-EN001", RarityName = "Secret Rare", DesiredQuantity = 3 },
                new PreferredVersion { CardID = 1, ImageID = 10, SetCode = "RA04-EN001", RarityName = "Quarter Century Secret Rare", DesiredQuantity = 1 }
            ]);

            var result = (await _service.GetWishlistAsync()).ToList();

            Assert.AreEqual(2, result.Count);
            var suda = result.Single(r => r.SetCode == "SUDA-EN001");
            var qcsr = result.Single(r => r.SetCode == "RA04-EN001");
            Assert.AreEqual(3, suda.QuantityNeeded);
            Assert.AreEqual(1, qcsr.QuantityNeeded);
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
