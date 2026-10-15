using CardCollector.DTO;
using CardCollector.Models.Catalog;
using CardCollector.Repository;
using CardCollector.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CardCollector.Tests.Services
{
    [TestClass]
    public sealed class PricingDataCacheTests
    {
        private Mock<ICardDataRepository> _cardDataRepositoryMock = null!;
        private Mock<ITCGCatalogCache> _tcgCatalogCacheMock = null!;

        [TestMethod]
        public void BuildIndex_MultipleEditionsOfOneProduct_GroupsTogether()
        {
            var printings = new List<TCGPriceSet>
            {
                new() { Edition = "1st Edition", PriceRaw = "5", ProductID = 500 },
                new() { Edition = "Unlimited", PriceRaw = "3", ProductID = 500 }
            };

            var index = PricingDataCache.BuildIndex(printings);

            Assert.AreEqual(2, index[500].Count);
        }

        [TestMethod]
        public void BuildIndex_RowWithoutProductID_IsLeftOut()
        {
            var printings = new List<TCGPriceSet> { new() { Code = "SYN-EN001", PriceRaw = "5" } };

            var index = PricingDataCache.BuildIndex(printings);

            Assert.AreEqual(0, index.Count);
        }

        [TestMethod]
        public void GetPrintingRows_PrintingWithLinkedProducts_ReturnsEveryProductsRows()
        {
            var set = new Set { Code = "SYN-EN003", PrintingID = 101, RarityName = "Ultra Rare", TCGProductIDs = [500, 501] };
            var card = new Card { ID = 1, CardSets = [set] };
            _cardDataRepositoryMock.Setup(r => r.GetPrintingByID(101)).Returns(new CatalogPrintingRef(card, set));
            _tcgCatalogCacheMock.Setup(c => c.GetAllPrintings()).Returns(
            [
                new TCGPriceSet { Edition = "1st Edition", PriceRaw = "12.82", ProductID = 500 },
                new TCGPriceSet { Edition = "Unlimited", PriceRaw = "3.10", ProductID = 501 },
                new TCGPriceSet { Edition = "Unlimited", PriceRaw = "99.00", ProductID = 777 }
            ]);
            var cache = new PricingDataCache(_cardDataRepositoryMock.Object, _tcgCatalogCacheMock.Object);

            var rows = cache.GetPrintingRows(101);

            CollectionAssert.AreEquivalent(new[] { 12.82m, 3.10m }, rows.Select(r => r.Price).ToArray());
        }

        [TestMethod]
        public void GetPrintingRows_UnknownPrinting_ReturnsEmpty()
        {
            _tcgCatalogCacheMock.Setup(c => c.GetAllPrintings()).Returns([]);
            var cache = new PricingDataCache(_cardDataRepositoryMock.Object, _tcgCatalogCacheMock.Object);

            var rows = cache.GetPrintingRows(999);

            Assert.AreEqual(0, rows.Count);
        }

        [TestMethod]
        public void GetPrintingRowsByCard_LeavesOutPrintingsWithoutRows()
        {
            var card = new Card
            {
                ID = 1,
                CardSets =
                [
                    new Set { Code = "SYN-EN003", PrintingID = 101, TCGProductIDs = [500] },
                    new Set { Code = "SYN-EN003", PrintingID = 102, PrintVariant = "Extended Art" }
                ]
            };
            _cardDataRepositoryMock.Setup(r => r.GetCardByID(1)).Returns(card);
            _tcgCatalogCacheMock.Setup(c => c.GetAllPrintings()).Returns([new TCGPriceSet { Edition = "Unlimited", PriceRaw = "1.00", ProductID = 500 }]);
            var cache = new PricingDataCache(_cardDataRepositoryMock.Object, _tcgCatalogCacheMock.Object);

            var rows = cache.GetPrintingRowsByCard(1);

            CollectionAssert.AreEquivalent(new[] { 101 }, rows.Keys.ToArray());
        }

        [TestMethod]
        public void LookupPrintingRows_NullPrinting_ReturnsEmpty()
        {
            var rows = PricingDataCache.LookupPrintingRows(null, new Dictionary<int, IReadOnlyList<TCGPriceSet>>());

            Assert.AreEqual(0, rows.Count);
        }

        [TestMethod]
        public void RebuildIndex_CalledAfterCatalogPricesChange_ReflectsNewPrices()
        {
            _tcgCatalogCacheMock.SetupSequence(c => c.GetAllPrintings())
                .Returns([new TCGPriceSet { PriceRaw = "5", ProductID = 500 }])
                .Returns([new TCGPriceSet { PriceRaw = "12.82", ProductID = 500 }]);
            var set = new Set { Code = "SYN-EN003", PrintingID = 101, RarityName = "Ultra Rare", TCGProductIDs = [500] };
            _cardDataRepositoryMock.Setup(r => r.GetPrintingByID(101)).Returns(new CatalogPrintingRef(new Card { ID = 1, CardSets = [set] }, set));
            var cache = new PricingDataCache(_cardDataRepositoryMock.Object, _tcgCatalogCacheMock.Object);

            cache.RebuildIndex();
            var result = cache.GetPrintingRows(101);

            Assert.AreEqual(12.82m, result.Single().Price);
        }

        [TestInitialize]
        public void Setup()
        {
            _cardDataRepositoryMock = new Mock<ICardDataRepository>();
            _tcgCatalogCacheMock = new Mock<ITCGCatalogCache>();
        }
    }
}
