using CardCollector.Data.Models;
using CardCollector.DTO;
using CardCollector.Services;
using Moq;

namespace CardCollector.Tests.Services
{
    [TestClass]
    public sealed class PricingServiceTests
    {
        private Mock<IPricingDataCache> _pricingDataCacheMock = null!;
        private PricingService _service = null!;

        [TestMethod]
        public async Task GetEditionsByPrintingAsync_MapsEachPrintingToItsListedEditions()
        {
            _pricingDataCacheMock.Setup(c => c.GetPrintingRowsByCard(1)).Returns(new Dictionary<int, IReadOnlyList<TCGPriceSet>>
            {
                [101] = [MakeSet("LOB-EN001", "Ultra Rare", "1st Edition", "10"), MakeSet("LOB-EN001", "Ultra Rare", "Unlimited", "5")],
                [102] = [MakeSet("LOB-EN001", "Ultra Rare", "Unlimited", "8", "Extended Art")]
            });

            var map = await _service.GetEditionsByPrintingAsync(1);

            CollectionAssert.AreEquivalent(new[] { CardEdition.FirstEdition, CardEdition.Unlimited }, map[101].ToArray());
            CollectionAssert.AreEquivalent(new[] { CardEdition.Unlimited }, map[102].ToArray());
        }

        [TestMethod]
        public async Task GetEditionsByPrintingAsync_UnparseableEditionString_IsLeftOut()
        {
            _pricingDataCacheMock.Setup(c => c.GetPrintingRowsByCard(1)).Returns(new Dictionary<int, IReadOnlyList<TCGPriceSet>>
            {
                [101] = [MakeSet("LOB-EN001", "Ultra Rare", "Not An Edition", "10"), MakeSet("LOB-EN001", "Ultra Rare", "1st Edition", "5")]
            });

            var map = await _service.GetEditionsByPrintingAsync(1);

            CollectionAssert.AreEquivalent(new[] { CardEdition.FirstEdition }, map[101].ToArray());
        }

        [TestMethod]
        public async Task GetPrintingPriceAsync_ListedPriceIsZero_ReturnsNull()
        {
            _pricingDataCacheMock.Setup(c => c.GetPrintingRows(101)).Returns([MakeSet("LOB-EN001", "Ultra Rare", "1st Edition", "0")]);

            var price = await _service.GetPrintingPriceAsync(101, CardEdition.FirstEdition);

            Assert.IsNull(price);
        }

        [TestMethod]
        public async Task GetPrintingPriceAsync_PrintingEditionListed_ReturnsThatEditionsPrice()
        {
            _pricingDataCacheMock.Setup(c => c.GetPrintingRows(101))
                .Returns([MakeSet("LOB-EN001", "Ultra Rare", "Unlimited", "5"), MakeSet("LOB-EN001", "Ultra Rare", "1st Edition", "10")]);

            var price = await _service.GetPrintingPriceAsync(101, CardEdition.FirstEdition);

            Assert.AreEqual(10m, price);
        }

        [TestMethod]
        public async Task GetPrintingPriceAsync_PrintingEditionNotListed_FallsBackToAnyEdition()
        {
            _pricingDataCacheMock.Setup(c => c.GetPrintingRows(101)).Returns([MakeSet("LOB-EN001", "Ultra Rare", "Unlimited", "5")]);

            var price = await _service.GetPrintingPriceAsync(101, CardEdition.FirstEdition);

            Assert.AreEqual(5m, price);
        }

        [TestMethod]
        public async Task GetPrintingPriceAsync_PrintingWithoutLinkedProduct_ReturnsNull()
        {
            _pricingDataCacheMock.Setup(c => c.GetPrintingRows(101)).Returns([]);

            var price = await _service.GetPrintingPriceAsync(101, CardEdition.FirstEdition);

            Assert.IsNull(price);
        }

        [TestInitialize]
        public void Setup()
        {
            _pricingDataCacheMock = new Mock<IPricingDataCache>();
            _service = new PricingService(_pricingDataCacheMock.Object);
        }

        private static TCGPriceSet MakeSet(string code, string rarityName, string edition, string priceRaw, string? printVariant = null) => new()
        {
            Code = code,
            RarityName = rarityName,
            Edition = edition,
            PriceRaw = priceRaw,
            PrintVariant = printVariant
        };
    }
}
