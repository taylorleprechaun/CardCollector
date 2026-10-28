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
            SetUpPrinting(101, "LOB-EN001", "Ultra Rare");
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CatalogPrintingID = 101 }
            ]);

            await _service.GetWishlistAsync();
            await _service.GetWishlistAsync();

            _preferredVersionRepositoryMock.Verify(r => r.GetAllAsync(), Times.Once());
            _collectionRepositoryMock.Verify(r => r.GetOwnedQuantitiesByPrintingAsync(It.IsAny<IEnumerable<int>>()), Times.Once());
            _collectionRepositoryMock.Verify(r => r.GetOrderedQuantitiesAsync(), Times.Once());
            _pendingOrderRepositoryMock.Verify(r => r.GetStagedQuantitiesAsync(), Times.Once());
        }

        [TestMethod]
        public async Task GetWishlistAsync_MultipleItems_OrdersByCardNameThenSetCode()
        {
            SetUpCard(1, "Zeta Card", new Set { Code = "ZZZ-EN001", PrintingID = 101, RarityName = "Common" });
            SetUpCard(2, "Alpha Card", new Set { Code = "AAA-EN001", PrintingID = 201, RarityName = "Common" });
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CatalogPrintingID = 101 },
                new PreferredVersion { CatalogPrintingID = 201 }
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
        [DataRow(1, 1, DisplayName = "Custom target of 1 met")]
        [DataRow(3, 3, DisplayName = "Default target of 3 met")]
        public async Task GetWishlistAsync_OwnedQuantityMeetsTarget_ExcludesItem(int desiredQuantity, int owned)
        {
            SetUpPrinting(101, "LOB-EN001", "Ultra Rare");
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CatalogPrintingID = 101, DesiredQuantity = desiredQuantity }
            ]);
            _collectionRepositoryMock.Setup(r => r.GetOwnedQuantitiesByPrintingAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync(new Dictionary<int, int> { [101] = owned });

            var result = await _service.GetWishlistAsync();

            Assert.AreEqual(0, result.Count());
        }

        [TestMethod]
        public async Task GetWishlistAsync_OwnedQuantityBelowThreshold_IncludesItemWithItsPrintingsQuantities()
        {
            SetUpPrinting(101, "LOB-EN001", "Ultra Rare");
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CatalogPrintingID = 101 }
            ]);
            _collectionRepositoryMock.Setup(r => r.GetOwnedQuantitiesByPrintingAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync(new Dictionary<int, int> { [101] = 1 });
            _collectionRepositoryMock.Setup(r => r.GetOrderedQuantitiesAsync()).ReturnsAsync(new Dictionary<int, int> { [101] = 1, [102] = 5 });
            _pendingOrderRepositoryMock.Setup(r => r.GetStagedQuantitiesAsync()).ReturnsAsync(new Dictionary<int, int> { [101] = 1, [102] = 5 });

            var result = (await _service.GetWishlistAsync()).ToList();

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(101, result[0].PrintingID);
            Assert.AreEqual(1, result[0].QuantityOwned);
            Assert.AreEqual(1, result[0].OrderedQuantity);
            Assert.AreEqual(1, result[0].CartQuantity);
        }

        [TestMethod]
        public async Task GetWishlistAsync_OwnedCopiesOfAVariant_DoNotCountTowardThePlainPrinting()
        {
            SetUpCard(1, "Synthetic Dragon",
                new Set { Code = "RA05-EN146", PrintingID = 101, RarityName = "Ultra Rare" },
                new Set { Code = "RA05-EN146", PrintingID = 102, PrintVariant = "Extended Art", RarityName = "Ultra Rare" });
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CatalogPrintingID = 101 }
            ]);
            _collectionRepositoryMock.Setup(r => r.GetOwnedQuantitiesByPrintingAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync(new Dictionary<int, int> { [102] = 3 });

            var result = (await _service.GetWishlistAsync()).ToList();

            Assert.AreEqual(0, result.Single().QuantityOwned);
        }

        [TestMethod]
        public async Task GetWishlistAsync_PrintingFromCatalog_ResolvesPriceSetNameAndRarityCode()
        {
            SetUpCard(1, "Dark Magician", new Set { Code = "LOB-EN001", Name = "Legend of Blue Eyes White Dragon", Price = 5m, PrintingID = 101, RarityName = "Common" });
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CatalogPrintingID = 101 }
            ]);

            var result = (await _service.GetWishlistAsync()).Single();

            Assert.AreEqual("Common", result.RarityName);
            Assert.AreEqual("(C)", result.RarityCode);
            Assert.AreEqual("Legend of Blue Eyes White Dragon", result.SetName);
            Assert.AreEqual(5m, result.Price);
        }

        [TestMethod]
        public async Task GetWishlistAsync_TwoTrackedPrintingsForSameCard_BothAppearIndependently()
        {
            SetUpCard(1, "A Bao A Qu, the Lightless Shadow",
                new Set { Code = "SUDA-EN001", PrintingID = 101, RarityName = "Secret Rare" },
                new Set { Code = "RA04-EN001", PrintingID = 102, RarityName = "Quarter Century Secret Rare" });
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CatalogPrintingID = 101, DesiredQuantity = 3 },
                new PreferredVersion { CatalogPrintingID = 102, DesiredQuantity = 1 }
            ]);

            var result = (await _service.GetWishlistAsync()).ToList();

            Assert.AreEqual(3, result.Single(r => r.SetCode == "SUDA-EN001").QuantityNeeded);
            Assert.AreEqual(1, result.Single(r => r.SetCode == "RA04-EN001").QuantityNeeded);
        }

        [TestMethod]
        public async Task GetWishlistDistinctRarityNamesAsync_ReturnsSortedDistinctNonEmptyNames()
        {
            SetUpCard(1, "Card A", new Set { Code = "LOB-EN001", PrintingID = 101, RarityName = "Ultra Rare" });
            SetUpCard(2, "Card B", new Set { Code = "LOB-EN002", PrintingID = 201, RarityName = "Common" });
            SetUpCard(3, "Card C", new Set { Code = "LOB-EN003", PrintingID = 301, RarityName = "Ultra Rare" });
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CatalogPrintingID = 101 },
                new PreferredVersion { CatalogPrintingID = 201 },
                new PreferredVersion { CatalogPrintingID = 301 }
            ]);

            var result = await _service.GetWishlistDistinctRarityNamesAsync();

            CollectionAssert.AreEqual(new[] { "Common", "Ultra Rare" }, result.ToArray());
        }

        [TestMethod]
        public async Task GetWishlistDistinctSetNamesAsync_MapsSetCodeToCanonicalNameWhenKnown()
        {
            SetUpPrinting(101, "LOB-EN001", "Ultra Rare");
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CatalogPrintingID = 101 }
            ]);
            _cardDataRepositoryMock.Setup(r => r.GetSetNamesByCode())
                .Returns(new Dictionary<string, string> { ["LOB-EN001"] = "Legend of Blue Eyes White Dragon" });

            var result = await _service.GetWishlistDistinctSetNamesAsync();

            CollectionAssert.AreEqual(new[] { "Legend of Blue Eyes White Dragon" }, result.ToArray());
        }

        [TestMethod]
        public async Task GetWishlistItemAsync_FullyOwned_ReturnsNull()
        {
            SetUpPrinting(101, "LOB-EN001", "Ultra Rare");
            _preferredVersionRepositoryMock.Setup(r => r.GetByIDAsync(7))
                .ReturnsAsync(new PreferredVersion { ID = 7, CatalogPrintingID = 101, DesiredQuantity = 2 });
            _collectionRepositoryMock.Setup(r => r.GetOwnedQuantitiesByPrintingAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync(new Dictionary<int, int> { [101] = 2 });

            var result = await _service.GetWishlistItemAsync(7);

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetWishlistItemAsync_NoSuchTrackedPrinting_ReturnsNull()
        {
            var result = await _service.GetWishlistItemAsync(999);

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetWishlistItemAsync_StillNeeded_BuildsTheRowAndLooksUpOnlyThatPrinting()
        {
            SetUpPrinting(101, "LOB-EN001", "Ultra Rare");
            _preferredVersionRepositoryMock.Setup(r => r.GetByIDAsync(7))
                .ReturnsAsync(new PreferredVersion { ID = 7, CatalogPrintingID = 101, DesiredQuantity = 3 });
            _collectionRepositoryMock.Setup(r => r.GetOwnedQuantitiesByPrintingAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync(new Dictionary<int, int> { [101] = 1 });
            _collectionRepositoryMock.Setup(r => r.GetOrderedQuantitiesAsync()).ReturnsAsync(new Dictionary<int, int> { [101] = 1 });
            _pendingOrderRepositoryMock.Setup(r => r.GetStagedQuantitiesAsync()).ReturnsAsync(new Dictionary<int, int> { [101] = 1 });

            var result = await _service.GetWishlistItemAsync(7);

            Assert.AreEqual(7, result!.PreferredVersionID);
            Assert.AreEqual("Dark Magician", result.CardName);
            Assert.AreEqual(1, result.QuantityOwned);
            Assert.AreEqual(1, result.OrderedQuantity);
            Assert.AreEqual(1, result.CartQuantity);
            _collectionRepositoryMock.Verify(r => r.GetOwnedQuantitiesByPrintingAsync(It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { 101 }))), Times.Once);
            _preferredVersionRepositoryMock.Verify(r => r.GetAllAsync(), Times.Never);
        }
    }
}
