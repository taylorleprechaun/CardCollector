using CardCollector.DTO;
using CardCollector.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CardCollector.Tests.Services
{
    [TestClass]
    public sealed class TCGCatalogCacheTests
    {
        [TestMethod]
        public void BuildGroupEntries_BasePrintNoQualifier_PrintVariantIsNull()
        {
            var products = new List<TCGCatalogProduct>
            {
                new()
                {
                    ProductID = 1,
                    Name = "Dark Magical Curtain",
                    ExtendedData = [new() { Name = "Number", Value = "MAMO-EN003" }, new() { Name = "Rarity", Value = "Ultra Rare" }]
                }
            };
            var prices = new List<TCGCatalogPrice> { new() { ProductID = 1, MarketPrice = 5m } };

            var result = TCGCatalogCache.BuildGroupEntries(products, prices).ToList();

            Assert.IsNull(result[0].PrintVariant);
        }

        [TestMethod]
        public void BuildGroupEntries_ExtendedArtVariant_PopulatesCardNameAndPrintVariant()
        {
            var products = new List<TCGCatalogProduct>
            {
                new()
                {
                    ProductID = 1,
                    Name = "Dark Magical Curtain (Extended Art)",
                    ExtendedData = [new() { Name = "Number", Value = "MAMO-EN003" }, new() { Name = "Rarity", Value = "Ultra Rare" }]
                }
            };
            var prices = new List<TCGCatalogPrice> { new() { ProductID = 1, MarketPrice = 12.82m } };

            var result = TCGCatalogCache.BuildGroupEntries(products, prices).ToList();

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("Dark Magical Curtain", result[0].CardName);
            Assert.AreEqual("Extended Art", result[0].PrintVariant);
            Assert.AreEqual("MAMO-EN003", result[0].Code);
            Assert.AreEqual("Ultra Rare", result[0].RarityName);
            Assert.AreEqual(12.82m, result[0].Price);
        }

        [TestMethod]
        public void BuildGroupEntries_MissingNumber_IsSkipped()
        {
            var products = new List<TCGCatalogProduct>
            {
                new() { ProductID = 1, Name = "Dark Magical Curtain", ExtendedData = [new TCGCatalogProductField { Name = "Rarity", Value = "Ultra Rare" }] }
            };
            var prices = new List<TCGCatalogPrice> { new() { ProductID = 1, MarketPrice = 5m } };

            var result = TCGCatalogCache.BuildGroupEntries(products, prices).ToList();

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void BuildGroupEntries_MissingRarity_IsSkipped()
        {
            var products = new List<TCGCatalogProduct>
            {
                new() { ProductID = 1, Name = "Dark Magical Curtain", ExtendedData = [new TCGCatalogProductField { Name = "Number", Value = "MAMO-EN003" }] }
            };
            var prices = new List<TCGCatalogPrice> { new() { ProductID = 1, MarketPrice = 5m } };

            var result = TCGCatalogCache.BuildGroupEntries(products, prices).ToList();

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void BuildGroupEntries_MultipleSubTypes_YieldsOneEntryPerSubType()
        {
            var products = new List<TCGCatalogProduct>
            {
                new()
                {
                    ProductID = 1,
                    Name = "Dark Magical Curtain",
                    ExtendedData = [new() { Name = "Number", Value = "MAMO-EN003" }, new() { Name = "Rarity", Value = "Ultra Rare" }]
                }
            };
            var prices = new List<TCGCatalogPrice>
            {
                new() { ProductID = 1, MarketPrice = 5m, SubTypeName = "1st Edition" },
                new() { ProductID = 1, MarketPrice = 4m, SubTypeName = "Unlimited" }
            };

            var result = TCGCatalogCache.BuildGroupEntries(products, prices).ToList();

            Assert.AreEqual(2, result.Count);
            CollectionAssert.AreEquivalent(new[] { "1st Edition", "Unlimited" }, result.Select(r => r.Edition).ToArray());
        }

        [TestMethod]
        public void BuildGroupEntries_NoMatchingProductID_YieldsNothing()
        {
            var products = new List<TCGCatalogProduct>
            {
                new()
                {
                    ProductID = 1,
                    Name = "Dark Magical Curtain",
                    ExtendedData = [new() { Name = "Number", Value = "MAMO-EN003" }, new() { Name = "Rarity", Value = "Ultra Rare" }]
                }
            };
            var prices = new List<TCGCatalogPrice> { new() { ProductID = 999, MarketPrice = 5m } };

            var result = TCGCatalogCache.BuildGroupEntries(products, prices).ToList();

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void BuildGroupEntries_NullMarketPrice_IsDropped()
        {
            var products = new List<TCGCatalogProduct>
            {
                new()
                {
                    ProductID = 1,
                    Name = "Dark Magical Curtain",
                    ExtendedData = [new() { Name = "Number", Value = "MAMO-EN003" }, new() { Name = "Rarity", Value = "Ultra Rare" }]
                }
            };
            var prices = new List<TCGCatalogPrice> { new() { ProductID = 1, MarketPrice = null } };

            var result = TCGCatalogCache.BuildGroupEntries(products, prices).ToList();

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void BuildGroupEntries_ProductWithGroup_CarriesProductAndGroupIDs()
        {
            var products = new List<TCGCatalogProduct>
            {
                new()
                {
                    GroupID = 24822,
                    ProductID = 713500,
                    Name = "Synthetic Dragon (Extended Art)",
                    ExtendedData = [new() { Name = "Number", Value = "SYN-EN001" }, new() { Name = "Rarity", Value = "Ultra Rare" }]
                }
            };
            var prices = new List<TCGCatalogPrice>
            {
                new() { ProductID = 713500, MarketPrice = 3m, SubTypeName = "1st Edition" },
                new() { ProductID = 713500, MarketPrice = 2m, SubTypeName = "Unlimited" }
            };

            var result = TCGCatalogCache.BuildGroupEntries(products, prices).ToList();

            Assert.AreEqual(2, result.Count);
            Assert.IsTrue(result.All(r => r.ProductID == 713500 && r.GroupID == 24822));
        }

        [TestMethod]
        public void BuildGroupEntries_QualifierIsProductsOwnSetCode_PrintVariantIsNull()
        {
            var products = new List<TCGCatalogProduct>
            {
                new()
                {
                    ProductID = 1,
                    Name = "Tri-Horned Dragon (LOB-000)",
                    ExtendedData = [new() { Name = "Number", Value = "LOB-000" }, new() { Name = "Rarity", Value = "Secret Rare" }]
                }
            };
            var prices = new List<TCGCatalogPrice> { new() { ProductID = 1, MarketPrice = 19.83m } };

            var result = TCGCatalogCache.BuildGroupEntries(products, prices).ToList();

            Assert.AreEqual("Tri-Horned Dragon", result[0].CardName);
            Assert.IsNull(result[0].PrintVariant);
        }

        [TestMethod]
        public void HasProductIDs_CacheWrittenBeforeProductIDs_ReturnsFalse()
        {
            var printings = new List<TCGPriceSet> { new() { Code = "SYN-EN001" }, new() { Code = "SYN-EN002" } };

            var result = TCGCatalogCache.HasProductIDs(printings);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void HasProductIDs_PrintingHasProductID_ReturnsTrue()
        {
            var printings = new List<TCGPriceSet> { new() { Code = "SYN-EN001" }, new() { Code = "SYN-EN002", ProductID = 713500 } };

            var result = TCGCatalogCache.HasProductIDs(printings);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void ParseProductName_NoParentheticalQualifier_ReturnsNameUnchangedAndNullVariant()
        {
            var (cardName, printVariant) = TCGCatalogCache.ParseProductName("Dark Magical Curtain", "Ultra Rare");

            Assert.AreEqual("Dark Magical Curtain", cardName);
            Assert.IsNull(printVariant);
        }

        [TestMethod]
        public void ParseProductName_NullRarityName_QualifierNeverTreatedAsRedundant()
        {
            var (cardName, printVariant) = TCGCatalogCache.ParseProductName("Dark Magical Curtain (Extended Art)", null);

            Assert.AreEqual("Dark Magical Curtain", cardName);
            Assert.AreEqual("Extended Art", printVariant);
        }

        [TestMethod]
        [DataRow("Synthetic Clown (Starfoil)", "Starfoil Rare", DisplayName = "Starfoil")]
        [DataRow("Synthetic Clown (Shatterfoil)", "Shatterfoil Rare", DisplayName = "Shatterfoil")]
        [DataRow("Synthetic Dragon (Secret)", "Secret Rare", DisplayName = "Secret")]
        public void ParseProductName_QualifierIsRarityWithoutRareSuffix_TreatedAsRedundant(string productName, string rarityName)
        {
            var (cardName, printVariant) = TCGCatalogCache.ParseProductName(productName, rarityName);

            Assert.IsNull(printVariant);
            Assert.IsFalse(cardName.Contains('('));
        }

        [TestMethod]
        public void ParseProductName_QualifierMatchesOwnSetCode_TreatedAsRedundant()
        {
            var (cardName, printVariant) = TCGCatalogCache.ParseProductName("Tri-Horned Dragon (LOB-000)", "Secret Rare", "LOB-000");

            Assert.AreEqual("Tri-Horned Dragon", cardName);
            Assert.IsNull(printVariant);
        }

        [TestMethod]
        public void ParseProductName_QualifierMatchesRarityAbbreviation_TreatedAsRedundant()
        {
            var (cardName, printVariant) = TCGCatalogCache.ParseProductName("Dark Magical Curtain (GMR)", "Grand Master Rare");

            Assert.AreEqual("Dark Magical Curtain", cardName);
            Assert.IsNull(printVariant);
        }

        [TestMethod]
        public void ParseProductName_QualifierMatchesRarityNameExactly_TreatedAsRedundant()
        {
            var (cardName, printVariant) = TCGCatalogCache.ParseProductName("Sphere Mode (Secret Rare)", "Secret Rare");

            Assert.AreEqual("Sphere Mode", cardName);
            Assert.IsNull(printVariant);
        }

        [TestMethod]
        public void ParseProductName_QualifierMatchesRarityWithDifferentApostrophe_TreatedAsRedundant()
        {
            var (cardName, printVariant) = TCGCatalogCache.ParseProductName("Synthetic God (Ultra Pharaoh's Rare)", "Ultra Pharaoh’s Rare");

            Assert.AreEqual("Synthetic God", cardName);
            Assert.IsNull(printVariant);
        }

        [TestMethod]
        public void ParseProductName_TwoTrailingQualifiers_StripsBothAndKeepsOnlyTheNonRedundantOne()
        {
            var (cardName, printVariant) = TCGCatalogCache.ParseProductName(
                "Dark Magical Curtain (Starlight Rare) (Extended Art)", "Starlight Rare");

            Assert.AreEqual("Dark Magical Curtain", cardName);
            Assert.AreEqual("Extended Art", printVariant);
        }
        [TestMethod]
        public void ParseProductName_UnrecognizedQualifier_IsKeptAsDistinctVariant()
        {
            var (cardName, printVariant) = TCGCatalogCache.ParseProductName("Some Card (Prerelease Promo)", "Ultra Rare");

            Assert.AreEqual("Some Card", cardName);
            Assert.AreEqual("Prerelease Promo", printVariant);
        }
    }
}
