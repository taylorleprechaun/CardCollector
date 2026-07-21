using CardCollector.DTO;
using CardCollector.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CardCollector.Tests.Services
{
    [TestClass]
    public sealed class PricingDataCacheTests
    {
        [TestMethod]
        public void IndexCards_CardWithEmptyCardSets_MapsToEmptyList()
        {
            var cards = new List<TCGPriceCard> { new() { ID = 5, CardSets = [] } };

            var result = PricingDataCache.IndexCards(cards);

            Assert.AreEqual(0, result[5].Count);
        }

        [TestMethod]
        public void IndexCards_DuplicateCardIDs_ThrowsArgumentException()
        {
            var cards = new List<TCGPriceCard>
            {
                new() { ID = 1, CardSets = [new TCGPriceSet { Code = "LOB" }] },
                new() { ID = 1, CardSets = [new TCGPriceSet { Code = "MRD" }] }
            };

            Assert.ThrowsExactly<ArgumentException>(() => PricingDataCache.IndexCards(cards));
        }

        [TestMethod]
        public void IndexCards_EmptyList_ReturnsEmptyDictionary()
        {
            var result = PricingDataCache.IndexCards([]);

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void IndexCards_MultipleCards_IndexesEachByID()
        {
            var cards = new List<TCGPriceCard>
            {
                new() { ID = 1, CardSets = [new TCGPriceSet { Code = "LOB" }] },
                new() { ID = 2, CardSets = [new TCGPriceSet { Code = "MRD" }] }
            };

            var result = PricingDataCache.IndexCards(cards);

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("LOB", result[1].Single().Code);
            Assert.AreEqual("MRD", result[2].Single().Code);
        }
    }
}
