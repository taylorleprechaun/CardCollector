using CardCollector.DTO;
using CardCollector.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CardCollector.Tests.Services
{
    [TestClass]
    public sealed class PricingDataCacheTests
    {
        [TestMethod]
        public void ApplyGroupEntries_ExistingKey_OverwritesRatherThanMerges()
        {
            var target = new Dictionary<(string SetCode, string RarityName), List<TCGPriceSet>>
            {
                [("MAMO-EN003", "ULTRA RARE")] = [new TCGPriceSet { Code = "MAMO-EN003", RarityName = "Ultra Rare", PriceRaw = "50" }]
            };
            var newGroupEntries = new List<TCGPriceSet> { new() { Code = "MAMO-EN003", RarityName = "Ultra Rare", PriceRaw = "12" } };

            PricingDataCache.ApplyGroupEntries(target, newGroupEntries);

            var entries = target[("MAMO-EN003", "ULTRA RARE")];
            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual(12m, entries[0].Price);
        }

        [TestMethod]
        public void ApplyGroupEntries_MultipleEditionsSameKey_GroupsTogether()
        {
            var target = new Dictionary<(string SetCode, string RarityName), List<TCGPriceSet>>();
            var entries = new List<TCGPriceSet>
            {
                new() { Code = "LOB-001", RarityName = "Ultra Rare", Edition = "1st Edition", PriceRaw = "100" },
                new() { Code = "LOB-001", RarityName = "Ultra Rare", Edition = "Unlimited", PriceRaw = "50" }
            };

            PricingDataCache.ApplyGroupEntries(target, entries);

            Assert.AreEqual(2, target[("LOB-001", "ULTRA RARE")].Count);
        }

        [TestMethod]
        public void ApplyGroupEntries_ShortPrintRarity_NormalizesToCommonKey()
        {
            var target = new Dictionary<(string SetCode, string RarityName), List<TCGPriceSet>>();
            var entries = new List<TCGPriceSet> { new() { Code = "ABC-EN001", RarityName = "Short Print", PriceRaw = "1" } };

            PricingDataCache.ApplyGroupEntries(target, entries);

            Assert.IsTrue(target.ContainsKey(("ABC-EN001", "COMMON")));
        }

        [TestMethod]
        public void BuildGroupEntries_MultipleSubTypeRowsForOneProduct_YieldsOneEntryPerEdition()
        {
            var products = new List<TCGCatalogProduct>
            {
                new()
                {
                    ProductID = 1,
                    ExtendedData =
                    [
                        new TCGCatalogProductField { Name = "Number", Value = "LOB-001" },
                        new TCGCatalogProductField { Name = "Rarity", Value = "Ultra Rare" }
                    ]
                }
            };
            var prices = new List<TCGCatalogPrice>
            {
                new() { ProductID = 1, MarketPrice = 128.54m, SubTypeName = "Unlimited" },
                new() { ProductID = 1, MarketPrice = 14.99m, SubTypeName = "1st Edition" }
            };

            var result = PricingDataCache.BuildGroupEntries(products, prices).ToList();

            Assert.AreEqual(2, result.Count);
            Assert.IsTrue(result.Any(r => r.Edition == "Unlimited" && r.Price == 128.54m));
            Assert.IsTrue(result.Any(r => r.Edition == "1st Edition" && r.Price == 14.99m));
        }

        [TestMethod]
        public void BuildGroupEntries_PriceWithNullMarketPrice_IsDropped()
        {
            var products = new List<TCGCatalogProduct>
            {
                new()
                {
                    ProductID = 1,
                    ExtendedData =
                    [
                        new TCGCatalogProductField { Name = "Number", Value = "MAMO-EN003" },
                        new TCGCatalogProductField { Name = "Rarity", Value = "Ultra Rare" }
                    ]
                }
            };
            var prices = new List<TCGCatalogPrice> { new() { ProductID = 1, MarketPrice = null, SubTypeName = "1st Edition" } };

            var result = PricingDataCache.BuildGroupEntries(products, prices).ToList();

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void BuildGroupEntries_ProductMissingNumber_IsSkipped()
        {
            var products = new List<TCGCatalogProduct>
            {
                new() { ProductID = 1, ExtendedData = [new TCGCatalogProductField { Name = "Rarity", Value = "Ultra Rare" }] }
            };
            var prices = new List<TCGCatalogPrice> { new() { ProductID = 1, MarketPrice = 5m } };

            var result = PricingDataCache.BuildGroupEntries(products, prices).ToList();

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void BuildGroupEntries_ProductMissingRarity_IsSkipped()
        {
            var products = new List<TCGCatalogProduct>
            {
                new() { ProductID = 1, ExtendedData = [new TCGCatalogProductField { Name = "Number", Value = "MAMO-EN003" }] }
            };
            var prices = new List<TCGCatalogPrice> { new() { ProductID = 1, MarketPrice = 5m } };

            var result = PricingDataCache.BuildGroupEntries(products, prices).ToList();

            Assert.AreEqual(0, result.Count);
        }
        [TestMethod]
        public void BuildGroupEntries_ProductWithNoMatchingPrice_YieldsNoEntries()
        {
            var products = new List<TCGCatalogProduct>
            {
                new()
                {
                    ProductID = 1,
                    ExtendedData =
                    [
                        new TCGCatalogProductField { Name = "Number", Value = "MAMO-EN003" },
                        new TCGCatalogProductField { Name = "Rarity", Value = "Ultra Rare" }
                    ]
                }
            };

            var result = PricingDataCache.BuildGroupEntries(products, []).ToList();

            Assert.AreEqual(0, result.Count);
        }
        [TestMethod]
        public void BuildGroupEntries_ValidProductAndPrice_ProducesExpectedPriceSet()
        {
            var products = new List<TCGCatalogProduct>
            {
                new()
                {
                    ProductID = 42,
                    ExtendedData =
                    [
                        new TCGCatalogProductField { Name = "Number", Value = "MAMO-EN003" },
                        new TCGCatalogProductField { Name = "Rarity", Value = "Ultra Rare" }
                    ]
                }
            };
            var prices = new List<TCGCatalogPrice> { new() { ProductID = 42, MarketPrice = 12.82m, SubTypeName = "1st Edition" } };

            var result = PricingDataCache.BuildGroupEntries(products, prices).Single();

            Assert.AreEqual("MAMO-EN003", result.Code);
            Assert.AreEqual("Ultra Rare", result.RarityName);
            Assert.AreEqual("1st Edition", result.Edition);
            Assert.AreEqual(12.82m, result.Price);
        }

        [TestMethod]
        public void LookupCardSets_CardWithNullCardSets_ReturnsEmptyList()
        {
            var card = new Card { ID = 1, CardSets = null };
            var index = new Dictionary<(string SetCode, string RarityName), IReadOnlyList<TCGPriceSet>>();

            var result = PricingDataCache.LookupCardSets(card, index);

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void LookupCardSets_MatchingSet_ReturnsIndexedPriceSets()
        {
            var priceSet = new TCGPriceSet { Code = "MAMO-EN003", RarityName = "Ultra Rare", PriceRaw = "12.82" };
            var card = new Card { ID = 1, CardSets = [new Set { Code = "MAMO-EN003", RarityName = "Ultra Rare" }] };
            var index = new Dictionary<(string SetCode, string RarityName), IReadOnlyList<TCGPriceSet>>
            {
                [("MAMO-EN003", "ULTRA RARE")] = [priceSet]
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
            var index = new Dictionary<(string SetCode, string RarityName), IReadOnlyList<TCGPriceSet>>
            {
                [("MAMO-EN003", "ULTRA RARE")] = [new TCGPriceSet { Code = "MAMO-EN003", RarityName = "Ultra Rare", PriceRaw = "12" }],
                [("MAMO-EN003", "SECRET RARE")] = [new TCGPriceSet { Code = "MAMO-EN003", RarityName = "Secret Rare", PriceRaw = "40" }]
            };

            var result = PricingDataCache.LookupCardSets(card, index);

            Assert.AreEqual(2, result.Count);
        }

        [TestMethod]
        public void LookupCardSets_NullCard_ReturnsEmptyList()
        {
            var index = new Dictionary<(string SetCode, string RarityName), IReadOnlyList<TCGPriceSet>>();

            var result = PricingDataCache.LookupCardSets(null, index);

            Assert.AreEqual(0, result.Count);
        }
        [TestMethod]
        public void LookupCardSets_SetWithNoMatchInIndex_IsSkipped()
        {
            var card = new Card { ID = 1, CardSets = [new Set { Code = "MAMO-EN003", RarityName = "Ultra Rare" }] };
            var index = new Dictionary<(string SetCode, string RarityName), IReadOnlyList<TCGPriceSet>>();

            var result = PricingDataCache.LookupCardSets(card, index);

            Assert.AreEqual(0, result.Count);
        }
        [TestMethod]
        public void LookupCardSets_ShortPrintRarityOnCard_MatchesCommonNormalizedKey()
        {
            var priceSet = new TCGPriceSet { Code = "ABC-EN001", RarityName = "Common", PriceRaw = "1" };
            var card = new Card { ID = 1, CardSets = [new Set { Code = "ABC-EN001", RarityName = "Short Print" }] };
            var index = new Dictionary<(string SetCode, string RarityName), IReadOnlyList<TCGPriceSet>>
            {
                [("ABC-EN001", "COMMON")] = [priceSet]
            };

            var result = PricingDataCache.LookupCardSets(card, index);

            Assert.AreEqual(1, result.Count);
        }
    }
}
