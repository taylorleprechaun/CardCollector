using CardCollector.DTO;
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
        public void BuildIndex_DifferentPrintVariants_KeyedSeparately()
        {
            var printings = new List<TCGPriceSet>
            {
                new() { Code = "MAMO-EN003", RarityName = "Ultra Rare", PriceRaw = "5", PrintVariant = null },
                new() { Code = "MAMO-EN003", RarityName = "Ultra Rare", PriceRaw = "12.82", PrintVariant = "Extended Art" }
            };

            var index = PricingDataCache.BuildIndex(printings);

            Assert.AreEqual(5m, index[("MAMO-EN003", "ULTRA RARE", null)].Single().Price);
            Assert.AreEqual(12.82m, index[("MAMO-EN003", "ULTRA RARE", "EXTENDED ART")].Single().Price);
        }

        [TestMethod]
        public void BuildIndex_DuplicateKeyAcrossEntries_GroupsThemTogether()
        {
            // Two tcgcsv groups can legitimately share a set code (e.g. old reprints) and each contribute an
            // entry for the same (SetCode, RarityName, PrintVariant) key — both are kept; PricingService.FindMatch
            // picks one deterministically rather than this layer silently dropping either.
            var printings = new List<TCGPriceSet>
            {
                new() { Code = "MAMO-EN003", RarityName = "Ultra Rare", PriceRaw = "50" },
                new() { Code = "MAMO-EN003", RarityName = "Ultra Rare", PriceRaw = "12" }
            };

            var index = PricingDataCache.BuildIndex(printings);

            var entries = index[("MAMO-EN003", "ULTRA RARE", null)];
            Assert.AreEqual(2, entries.Count);
            CollectionAssert.AreEquivalent(new[] { 50m, 12m }, entries.Select(e => e.Price).ToArray());
        }

        [TestMethod]
        public void BuildIndex_MultipleEditionsSameKey_GroupsTogether()
        {
            var printings = new List<TCGPriceSet>
            {
                new() { Code = "LOB-001", RarityName = "Ultra Rare", Edition = "1st Edition", PriceRaw = "100" },
                new() { Code = "LOB-001", RarityName = "Ultra Rare", Edition = "Unlimited", PriceRaw = "50" }
            };

            var index = PricingDataCache.BuildIndex(printings);

            Assert.AreEqual(2, index[("LOB-001", "ULTRA RARE", null)].Count);
        }

        [TestMethod]
        public void BuildIndex_ShortPrintRarity_NormalizesToCommonKey()
        {
            var printings = new List<TCGPriceSet> { new() { Code = "ABC-EN001", RarityName = "Short Print", PriceRaw = "1" } };

            var index = PricingDataCache.BuildIndex(printings);

            Assert.IsTrue(index.ContainsKey(("ABC-EN001", "COMMON", null)));
        }

        [TestMethod]
        public void GetCardSets_CardExistsWithMatchingPrinting_ReturnsPriceFromCatalog()
        {
            var card = new Card { ID = 1, CardSets = [new Set { Code = "MAMO-EN003", RarityName = "Ultra Rare" }] };
            _cardDataRepositoryMock.Setup(r => r.GetCardByID(1)).Returns(card);
            _tcgCatalogCacheMock.Setup(c => c.GetAllPrintings())
                .Returns([new TCGPriceSet { Code = "MAMO-EN003", RarityName = "Ultra Rare", PriceRaw = "12.82" }]);
            var cache = new PricingDataCache(_cardDataRepositoryMock.Object, _tcgCatalogCacheMock.Object);

            var result = cache.GetCardSets(1);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(12.82m, result[0].Price);
        }

        [TestMethod]
        public void LookupCardSets_CardWithNullCardSets_ReturnsEmptyList()
        {
            var card = new Card { ID = 1, CardSets = null };
            var index = new Dictionary<(string, string, string?), IReadOnlyList<TCGPriceSet>>();

            var result = PricingDataCache.LookupCardSets(card, index);

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void LookupCardSets_MatchingSet_ReturnsIndexedPriceSets()
        {
            var priceSet = new TCGPriceSet { Code = "MAMO-EN003", RarityName = "Ultra Rare", PriceRaw = "12.82" };
            var card = new Card { ID = 1, CardSets = [new Set { Code = "MAMO-EN003", RarityName = "Ultra Rare" }] };
            var index = new Dictionary<(string, string, string?), IReadOnlyList<TCGPriceSet>>
            {
                [("MAMO-EN003", "ULTRA RARE", null)] = [priceSet]
            };

            var result = PricingDataCache.LookupCardSets(card, index);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(12.82m, result[0].Price);
        }

        [TestMethod]
        public void LookupCardSets_MultipleSets_AggregatesMatchesAcrossAll()
        {
            var card = new Card
            {
                ID = 1,
                CardSets =
                [
                    new Set { Code = "MAMO-EN003", RarityName = "Ultra Rare" },
                    new Set { Code = "MAMO-EN003", RarityName = "Secret Rare" }
                ]
            };
            var index = new Dictionary<(string, string, string?), IReadOnlyList<TCGPriceSet>>
            {
                [("MAMO-EN003", "ULTRA RARE", null)] = [new TCGPriceSet { Code = "MAMO-EN003", RarityName = "Ultra Rare", PriceRaw = "12" }],
                [("MAMO-EN003", "SECRET RARE", null)] = [new TCGPriceSet { Code = "MAMO-EN003", RarityName = "Secret Rare", PriceRaw = "40" }]
            };

            var result = PricingDataCache.LookupCardSets(card, index);

            Assert.AreEqual(2, result.Count);
        }

        [TestMethod]
        public void LookupCardSets_NullCard_ReturnsEmptyList()
        {
            var index = new Dictionary<(string, string, string?), IReadOnlyList<TCGPriceSet>>();

            var result = PricingDataCache.LookupCardSets(null, index);

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void LookupCardSets_SetWithNoMatchInIndex_IsSkipped()
        {
            var card = new Card { ID = 1, CardSets = [new Set { Code = "MAMO-EN003", RarityName = "Ultra Rare" }] };
            var index = new Dictionary<(string, string, string?), IReadOnlyList<TCGPriceSet>>();

            var result = PricingDataCache.LookupCardSets(card, index);

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void LookupCardSets_SetWithPrintVariant_MatchesVariantSpecificEntryOnly()
        {
            var basePrint = new TCGPriceSet { Code = "MAMO-EN003", RarityName = "Ultra Rare", PriceRaw = "5" };
            var extendedArt = new TCGPriceSet { Code = "MAMO-EN003", RarityName = "Ultra Rare", PriceRaw = "12.82", PrintVariant = "Extended Art" };
            var card = new Card { ID = 1, CardSets = [new Set { Code = "MAMO-EN003", RarityName = "Ultra Rare", PrintVariant = "Extended Art" }] };
            var index = new Dictionary<(string, string, string?), IReadOnlyList<TCGPriceSet>>
            {
                [("MAMO-EN003", "ULTRA RARE", null)] = [basePrint],
                [("MAMO-EN003", "ULTRA RARE", "EXTENDED ART")] = [extendedArt]
            };

            var result = PricingDataCache.LookupCardSets(card, index);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(12.82m, result[0].Price);
        }

        [TestMethod]
        public void LookupCardSets_ShortPrintRarityOnCard_MatchesCommonNormalizedKey()
        {
            var priceSet = new TCGPriceSet { Code = "ABC-EN001", RarityName = "Common", PriceRaw = "1" };
            var card = new Card { ID = 1, CardSets = [new Set { Code = "ABC-EN001", RarityName = "Short Print" }] };
            var index = new Dictionary<(string, string, string?), IReadOnlyList<TCGPriceSet>>
            {
                [("ABC-EN001", "COMMON", null)] = [priceSet]
            };

            var result = PricingDataCache.LookupCardSets(card, index);

            Assert.AreEqual(1, result.Count);
        }

        [TestMethod]
        public async Task RefreshAsync_RebuildsIndexFromRefreshedCatalog()
        {
            _tcgCatalogCacheMock.SetupSequence(c => c.GetAllPrintings())
                .Returns([new TCGPriceSet { Code = "MAMO-EN003", RarityName = "Ultra Rare", PriceRaw = "5" }])
                .Returns([new TCGPriceSet { Code = "MAMO-EN003", RarityName = "Ultra Rare", PriceRaw = "12.82" }]);
            _tcgCatalogCacheMock.Setup(c => c.RefreshAsync()).Returns(Task.CompletedTask);
            var card = new Card { ID = 1, CardSets = [new Set { Code = "MAMO-EN003", RarityName = "Ultra Rare" }] };
            _cardDataRepositoryMock.Setup(r => r.GetCardByID(1)).Returns(card);
            var cache = new PricingDataCache(_cardDataRepositoryMock.Object, _tcgCatalogCacheMock.Object);

            await cache.RefreshAsync();
            var result = cache.GetCardSets(1);

            _tcgCatalogCacheMock.Verify(c => c.RefreshAsync(), Times.Once);
            Assert.AreEqual(12.82m, result[0].Price);
        }

        [TestInitialize]
        public void Setup()
        {
            _cardDataRepositoryMock = new Mock<ICardDataRepository>();
            _tcgCatalogCacheMock = new Mock<ITCGCatalogCache>();
        }
    }
}
