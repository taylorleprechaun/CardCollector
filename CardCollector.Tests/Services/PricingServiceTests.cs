using CardCollector.Data.Models;
using CardCollector.DTO;
using CardCollector.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CardCollector.Tests.Services
{
    [TestClass]
    public sealed class PricingServiceTests
    {
        private Mock<IPricingDataCache> _pricingDataCacheMock = null!;
        private PricingService _service = null!;

        [TestMethod]
        public async Task GetCardEditionMapAsync_GroupsByUppercasedSetCodeAndRarityName()
        {
            _pricingDataCacheMock.Setup(c => c.GetCardSets(1)).Returns([
                MakeSet("lob-en001", "ultra rare", "1st Edition", "10"),
                MakeSet("LOB-EN001", "Ultra Rare", "Unlimited", "5")
            ]);

            var map = await _service.GetCardEditionMapAsync(1);

            Assert.AreEqual(1, map.Count);
            var editions = map[("LOB-EN001", "ULTRA RARE")];
            CollectionAssert.AreEquivalent(new[] { CardEdition.FirstEdition, CardEdition.Unlimited }, editions.ToArray());
        }

        [TestMethod]
        public async Task GetCardEditionMapAsync_UnparseableEditionString_IsExcludedFromSet()
        {
            _pricingDataCacheMock.Setup(c => c.GetCardSets(1)).Returns([
                MakeSet("LOB-EN001", "Ultra Rare", "Not A Real Edition", "10")
            ]);

            var map = await _service.GetCardEditionMapAsync(1);

            Assert.AreEqual(0, map[("LOB-EN001", "ULTRA RARE")].Count);
        }

        [TestMethod]
        public async Task GetPrintingPriceAsync_EditionSpecified_PrefersExactEditionMatch()
        {
            _pricingDataCacheMock.Setup(c => c.GetCardSets(1)).Returns([
                MakeSet("LOB-EN001", "Ultra Rare", "Unlimited", "5.00"),
                MakeSet("LOB-EN001", "Ultra Rare", "1st Edition", "50.00")
            ]);

            var price = await _service.GetPrintingPriceAsync(1, "LOB-EN001", "Ultra Rare", CardEdition.FirstEdition);

            Assert.AreEqual(50.00m, price);
        }

        [TestMethod]
        public async Task GetPrintingPriceAsync_EditionSpecifiedButNoExactMatch_FallsBackToSetRarityOnlyMatch()
        {
            _pricingDataCacheMock.Setup(c => c.GetCardSets(1)).Returns([
                MakeSet("LOB-EN001", "Ultra Rare", "Unlimited", "5.00")
            ]);

            var price = await _service.GetPrintingPriceAsync(1, "LOB-EN001", "Ultra Rare", CardEdition.FirstEdition);

            Assert.AreEqual(5.00m, price);
        }

        [TestMethod]
        public async Task GetPrintingPriceAsync_MatchHasZeroPrice_ReturnsNull()
        {
            _pricingDataCacheMock.Setup(c => c.GetCardSets(1)).Returns([
                MakeSet("LOB-EN001", "Ultra Rare", "Unlimited", "0")
            ]);

            var price = await _service.GetPrintingPriceAsync(1, "LOB-EN001", "Ultra Rare");

            Assert.IsNull(price);
        }

        [TestMethod]
        public async Task GetPrintingPriceAsync_NoMatchingSetOrRarity_ReturnsNull()
        {
            _pricingDataCacheMock.Setup(c => c.GetCardSets(1)).Returns([]);

            var price = await _service.GetPrintingPriceAsync(1, "LOB-EN001", "Ultra Rare");

            Assert.IsNull(price);
        }

        [TestMethod]
        public async Task GetPrintingPriceAsync_SetRarityMatchIgnoringCase_ReturnsPrice()
        {
            _pricingDataCacheMock.Setup(c => c.GetCardSets(1)).Returns([
                MakeSet("lob-en001", "ultra rare", "Unlimited", "12.50")
            ]);

            var price = await _service.GetPrintingPriceAsync(1, "LOB-EN001", "Ultra Rare");

            Assert.AreEqual(12.50m, price);
        }

        [TestInitialize]
        public void Setup()
        {
            _pricingDataCacheMock = new Mock<IPricingDataCache>();
            _service = new PricingService(_pricingDataCacheMock.Object);
        }
        private static TCGPriceSet MakeSet(string code, string rarityName, string edition, string priceRaw) => new()
        {
            Code = code,
            RarityName = rarityName,
            Edition = edition,
            PriceRaw = priceRaw
        };
    }
}
