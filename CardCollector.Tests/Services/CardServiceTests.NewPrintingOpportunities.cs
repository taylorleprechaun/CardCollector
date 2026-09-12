using CardCollector.Data.Models;
using CardCollector.DTO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CardCollector.Tests.Services
{
    public partial class CardServiceTests
    {
        [TestMethod]
        public async Task GetNewPrintingOpportunitiesAsync_CardSetsIsNull_IsSkipped()
        {
            _cardDataRepositoryMock.Setup(r => r.GetCardByID(1)).Returns(new Card { ID = 1, Name = "Dark Magician" });
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CardID = 1, SetCode = "LOB-EN001", RarityName = "Ultra Rare" }
            ]);

            var result = await _service.GetNewPrintingOpportunitiesAsync();

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public async Task GetNewPrintingOpportunitiesAsync_IgnoredCardWithNoPreferredVersion_SurfacesWithNotYetTrackedLabel()
        {
            _cardDataRepositoryMock.Setup(r => r.GetCardByID(2)).Returns(new Card
            {
                ID = 2,
                Name = "Blue-Eyes White Dragon",
                CardSets = [new Set { Code = "SDK-EN001", RarityName = "Ultra Rare" }],
                CardImages = [new Image { ID = 20 }]
            });
            _cardSetRepositoryMock.Setup(r => r.GetTCGDateBySetCode("SDK-EN001")).Returns("2020-01-01");
            _ignoredCardRepositoryMock.Setup(r => r.GetAllAsync())
                .ReturnsAsync(new Dictionary<int, DateTime> { [2] = new DateTime(2015, 1, 1) });

            var result = await _service.GetNewPrintingOpportunitiesAsync();

            Assert.AreEqual(1, result.Count);
            Assert.IsTrue(result[0].IsIgnored);
            Assert.AreEqual("Not yet tracked", result[0].CurrentSetName);
        }

        [TestMethod]
        public async Task GetNewPrintingOpportunitiesAsync_MultipleOpportunities_OrderedByCardName()
        {
            _cardDataRepositoryMock.Setup(r => r.GetCardByID(1)).Returns(new Card
            {
                ID = 1,
                Name = "Zeta Card",
                CardSets = [new Set { Code = "LOB-EN001" }, new Set { Code = "NEW-EN001" }],
                CardImages = [new Image { ID = 10 }]
            });
            _cardDataRepositoryMock.Setup(r => r.GetCardByID(2)).Returns(new Card
            {
                ID = 2,
                Name = "Alpha Card",
                CardSets = [new Set { Code = "SDK-EN001" }, new Set { Code = "NEW-EN002" }],
                CardImages = [new Image { ID = 20 }]
            });
            _cardSetRepositoryMock.Setup(r => r.GetTCGDateBySetCode("LOB-EN001")).Returns("2015-01-01");
            _cardSetRepositoryMock.Setup(r => r.GetTCGDateBySetCode("NEW-EN001")).Returns("2020-01-01");
            _cardSetRepositoryMock.Setup(r => r.GetTCGDateBySetCode("SDK-EN001")).Returns("2015-01-01");
            _cardSetRepositoryMock.Setup(r => r.GetTCGDateBySetCode("NEW-EN002")).Returns("2020-01-01");
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CardID = 1, SetCode = "LOB-EN001" },
                new PreferredVersion { CardID = 2, SetCode = "SDK-EN001" }
            ]);

            var result = await _service.GetNewPrintingOpportunitiesAsync();

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("Alpha Card", result[0].CardName);
            Assert.AreEqual("Zeta Card", result[1].CardName);
        }

        [TestMethod]
        public async Task GetNewPrintingOpportunitiesAsync_NewerPrintingCatalogRarityIsShortPrint_DisplaysAsCommon()
        {
            _cardDataRepositoryMock.Setup(r => r.GetCardByID(1)).Returns(new Card
            {
                ID = 1,
                Name = "Dark Magician",
                CardSets = [new Set { Code = "LOB-EN001", RarityName = "Ultra Rare" }, new Set { Code = "NEW-EN001", RarityName = "Short Print" }],
                CardImages = [new Image { ID = 10 }]
            });
            _cardSetRepositoryMock.Setup(r => r.GetTCGDateBySetCode("LOB-EN001")).Returns("2015-01-01");
            _cardSetRepositoryMock.Setup(r => r.GetTCGDateBySetCode("NEW-EN001")).Returns("2020-01-01");
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CardID = 1, SetCode = "LOB-EN001", RarityName = "Ultra Rare" }
            ]);

            var result = await _service.GetNewPrintingOpportunitiesAsync();

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("Common", result[0].NewerPrintings[0].RarityName);
        }

        [TestMethod]
        public async Task GetNewPrintingOpportunitiesAsync_NewerPrintingDismissedAsCommonButCatalogRarityIsShortPrint_ExcludesOpportunity()
        {
            _cardDataRepositoryMock.Setup(r => r.GetCardByID(1)).Returns(new Card
            {
                ID = 1,
                Name = "Dark Magician",
                CardSets = [new Set { Code = "LOB-EN001", RarityName = "Ultra Rare" }, new Set { Code = "NEW-EN001", RarityName = "Short Print" }],
                CardImages = [new Image { ID = 10 }]
            });
            _cardSetRepositoryMock.Setup(r => r.GetTCGDateBySetCode("LOB-EN001")).Returns("2015-01-01");
            _cardSetRepositoryMock.Setup(r => r.GetTCGDateBySetCode("NEW-EN001")).Returns("2020-01-01");
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CardID = 1, SetCode = "LOB-EN001", RarityName = "Ultra Rare" }
            ]);
            _dismissedNewPrintingRepositoryMock.Setup(r => r.GetAllAsync())
                .ReturnsAsync(new HashSet<(int, string, string, string?)> { (1, "NEW-EN001", "Common", null) });

            var result = await _service.GetNewPrintingOpportunitiesAsync();

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public async Task GetNewPrintingOpportunitiesAsync_NewerPrintingExists_IsIncluded()
        {
            _cardDataRepositoryMock.Setup(r => r.GetCardByID(1)).Returns(new Card
            {
                ID = 1,
                Name = "Dark Magician",
                CardSets = [new Set { Code = "LOB-EN001", RarityName = "Ultra Rare" }, new Set { Code = "NEW-EN001", RarityName = "Secret Rare" }],
                CardImages = [new Image { ID = 10 }]
            });
            _cardSetRepositoryMock.Setup(r => r.GetTCGDateBySetCode("LOB-EN001")).Returns("2015-01-01");
            _cardSetRepositoryMock.Setup(r => r.GetTCGDateBySetCode("NEW-EN001")).Returns("2020-01-01");
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CardID = 1, SetCode = "LOB-EN001", RarityName = "Ultra Rare" }
            ]);

            var result = await _service.GetNewPrintingOpportunitiesAsync();

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(1, result[0].NewerPrintings.Count);
            Assert.AreEqual("NEW-EN001", result[0].NewerPrintings[0].SetCode);
            Assert.IsFalse(result[0].IsIgnored);
        }

        [TestMethod]
        public async Task GetNewPrintingOpportunitiesAsync_NewerPrintingIsDismissed_ExcludesOpportunity()
        {
            _cardDataRepositoryMock.Setup(r => r.GetCardByID(1)).Returns(new Card
            {
                ID = 1,
                Name = "Dark Magician",
                CardSets = [new Set { Code = "LOB-EN001", RarityName = "Ultra Rare" }, new Set { Code = "NEW-EN001", RarityName = "Secret Rare" }],
                CardImages = [new Image { ID = 10 }]
            });
            _cardSetRepositoryMock.Setup(r => r.GetTCGDateBySetCode("LOB-EN001")).Returns("2015-01-01");
            _cardSetRepositoryMock.Setup(r => r.GetTCGDateBySetCode("NEW-EN001")).Returns("2020-01-01");
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CardID = 1, SetCode = "LOB-EN001", RarityName = "Ultra Rare" }
            ]);
            _dismissedNewPrintingRepositoryMock.Setup(r => r.GetAllAsync())
                .ReturnsAsync(new HashSet<(int, string, string, string?)> { (1, "NEW-EN001", "Secret Rare", null) });

            var result = await _service.GetNewPrintingOpportunitiesAsync();

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public async Task GetNewPrintingOpportunitiesAsync_NewerSetHasTwoPrintVariantsOfSameRarity_BothSurfaceWithDistinctVariants()
        {
            _cardDataRepositoryMock.Setup(r => r.GetCardByID(1)).Returns(new Card
            {
                ID = 1,
                Name = "Dark Magical Curtain",
                CardSets =
                [
                    new Set { Code = "LOB-EN001", RarityName = "Ultra Rare" },
                    new Set { Code = "RA05-EN001", RarityName = "Ultra Rare", PrintVariant = "Extended Art" },
                    new Set { Code = "RA05-EN001", RarityName = "Ultra Rare", PrintVariant = "Starlight Rare" }
                ],
                CardImages = [new Image { ID = 10 }]
            });
            _cardSetRepositoryMock.Setup(r => r.GetTCGDateBySetCode("LOB-EN001")).Returns("2015-01-01");
            _cardSetRepositoryMock.Setup(r => r.GetTCGDateBySetCode("RA05-EN001")).Returns("2020-01-01");
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CardID = 1, SetCode = "LOB-EN001", RarityName = "Ultra Rare" }
            ]);

            var result = await _service.GetNewPrintingOpportunitiesAsync();

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(2, result[0].NewerPrintings.Count);
            var variants = result[0].NewerPrintings.Select(p => p.PrintVariant).OrderBy(v => v).ToList();
            CollectionAssert.AreEqual(new[] { "Extended Art", "Starlight Rare" }, variants);
        }

        [TestMethod]
        public async Task GetNewPrintingOpportunitiesAsync_NoNewerPrintings_IsExcluded()
        {
            _cardDataRepositoryMock.Setup(r => r.GetCardByID(1)).Returns(new Card
            {
                ID = 1,
                Name = "Dark Magician",
                CardSets = [new Set { Code = "LOB-EN001", RarityName = "Ultra Rare" }],
                CardImages = [new Image { ID = 10 }]
            });
            _cardSetRepositoryMock.Setup(r => r.GetTCGDateBySetCode("LOB-EN001")).Returns("2015-01-01");
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CardID = 1, SetCode = "LOB-EN001", RarityName = "Ultra Rare" }
            ]);

            var result = await _service.GetNewPrintingOpportunitiesAsync();

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public async Task GetNewPrintingOpportunitiesAsync_OneVariantDismissed_SiblingVariantStillSurfaces()
        {
            _cardDataRepositoryMock.Setup(r => r.GetCardByID(1)).Returns(new Card
            {
                ID = 1,
                Name = "Dark Magical Curtain",
                CardSets =
                [
                    new Set { Code = "LOB-EN001", RarityName = "Ultra Rare" },
                    new Set { Code = "RA05-EN001", RarityName = "Ultra Rare", PrintVariant = "Extended Art" },
                    new Set { Code = "RA05-EN001", RarityName = "Ultra Rare", PrintVariant = "Starlight Rare" }
                ],
                CardImages = [new Image { ID = 10 }]
            });
            _cardSetRepositoryMock.Setup(r => r.GetTCGDateBySetCode("LOB-EN001")).Returns("2015-01-01");
            _cardSetRepositoryMock.Setup(r => r.GetTCGDateBySetCode("RA05-EN001")).Returns("2020-01-01");
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CardID = 1, SetCode = "LOB-EN001", RarityName = "Ultra Rare" }
            ]);
            _dismissedNewPrintingRepositoryMock.Setup(r => r.GetAllAsync())
                .ReturnsAsync(new HashSet<(int, string, string, string?)> { (1, "RA05-EN001", "Ultra Rare", "Extended Art") });

            var result = await _service.GetNewPrintingOpportunitiesAsync();

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(1, result[0].NewerPrintings.Count);
            Assert.AreEqual("Starlight Rare", result[0].NewerPrintings[0].PrintVariant);
        }

        [TestMethod]
        public async Task GetNewPrintingOpportunitiesAsync_PreferredVersionAlreadyTracksExactVariant_ExcludesThatVariantOnly()
        {
            _cardDataRepositoryMock.Setup(r => r.GetCardByID(1)).Returns(new Card
            {
                ID = 1,
                Name = "Dark Magical Curtain",
                CardSets =
                [
                    new Set { Code = "RA05-EN001", RarityName = "Ultra Rare", PrintVariant = "Extended Art" },
                    new Set { Code = "RA05-EN001", RarityName = "Ultra Rare", PrintVariant = "Starlight Rare" }
                ],
                CardImages = [new Image { ID = 10 }]
            });
            _cardSetRepositoryMock.Setup(r => r.GetTCGDateBySetCode("RA05-EN001")).Returns("2015-01-01");
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CardID = 1, SetCode = "RA05-EN001", RarityName = "Ultra Rare", PrintVariant = "Extended Art" }
            ]);

            var result = await _service.GetNewPrintingOpportunitiesAsync();

            Assert.AreEqual(0, result.Count);
        }
        [TestMethod]
        public async Task GetNewPrintingOpportunitiesAsync_PreferredVersionHasPrintVariant_CurrentPrintVariantIsPopulated()
        {
            _cardDataRepositoryMock.Setup(r => r.GetCardByID(1)).Returns(new Card
            {
                ID = 1,
                Name = "Dark Magical Curtain",
                CardSets =
                [
                    new Set { Code = "RA05-EN001", RarityName = "Ultra Rare", PrintVariant = "Extended Art" },
                    new Set { Code = "RA06-EN001", RarityName = "Ultra Rare", PrintVariant = "Extended Art" }
                ],
                CardImages = [new Image { ID = 10 }]
            });
            _cardSetRepositoryMock.Setup(r => r.GetTCGDateBySetCode("RA05-EN001")).Returns("2015-01-01");
            _cardSetRepositoryMock.Setup(r => r.GetTCGDateBySetCode("RA06-EN001")).Returns("2020-01-01");
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CardID = 1, SetCode = "RA05-EN001", RarityName = "Ultra Rare", PrintVariant = "Extended Art" }
            ]);

            var result = await _service.GetNewPrintingOpportunitiesAsync();

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("Extended Art", result[0].CurrentPrintVariant);
        }
        [TestMethod]
        public async Task GetNewPrintingOpportunitiesAsync_PreferredVersionIsCommonAndCatalogRarityIsShortPrintForSameSet_NotFlaggedAsNewerPrinting()
        {
            _cardDataRepositoryMock.Setup(r => r.GetCardByID(1)).Returns(new Card
            {
                ID = 1,
                Name = "Dark Magician",
                CardSets = [new Set { Code = "LOB-EN001", RarityName = "Short Print" }],
                CardImages = [new Image { ID = 10 }]
            });
            _cardSetRepositoryMock.Setup(r => r.GetTCGDateBySetCode("LOB-EN001")).Returns("2015-01-01");
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CardID = 1, SetCode = "LOB-EN001", RarityName = "Common" }
            ]);

            var result = await _service.GetNewPrintingOpportunitiesAsync();

            Assert.AreEqual(0, result.Count);
        }
    }
}
