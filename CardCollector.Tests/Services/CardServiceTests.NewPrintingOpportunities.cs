using CardCollector.Data.Models;
using CardCollector.DTO;
using Moq;

namespace CardCollector.Tests.Services
{
    public partial class CardServiceTests
    {
        [TestMethod]
        public async Task GetNewPrintingOpportunitiesAsync_IgnoredCardWithNoPreferredVersion_SurfacesWithNotYetTrackedLabel()
        {
            SetUpCard(2, "Blue-Eyes White Dragon", new Set { Code = "SDK-EN001", PrintingID = 201, RarityName = "Ultra Rare" });
            _cardSetRepositoryMock.Setup(r => r.GetTCGDateBySetCode("SDK-EN001")).Returns("2020-01-01");
            _ignoredCardRepositoryMock.Setup(r => r.GetAllAsync())
                .ReturnsAsync(new Dictionary<int, DateTime> { [2] = new DateTime(2015, 1, 1) });

            var result = await _service.GetNewPrintingOpportunitiesAsync();

            var opportunity = result.Single();
            Assert.IsTrue(opportunity.IsIgnored);
            Assert.AreEqual("Not yet tracked", opportunity.CurrentSetName);
            Assert.AreEqual(201, opportunity.NewerPrintings.Single().PrintingID);
        }

        [TestMethod]
        public async Task GetNewPrintingOpportunitiesAsync_MultipleOpportunities_OrderedByCardName()
        {
            SetUpCard(1, "Zeta Card", new Set { Code = "LOB-EN001", PrintingID = 101 }, new Set { Code = "NEW-EN001", PrintingID = 102 });
            SetUpCard(2, "Alpha Card", new Set { Code = "SDK-EN001", PrintingID = 201 }, new Set { Code = "NEW-EN002", PrintingID = 202 });
            _cardSetRepositoryMock.Setup(r => r.GetTCGDateBySetCode("LOB-EN001")).Returns("2015-01-01");
            _cardSetRepositoryMock.Setup(r => r.GetTCGDateBySetCode("NEW-EN001")).Returns("2020-01-01");
            _cardSetRepositoryMock.Setup(r => r.GetTCGDateBySetCode("SDK-EN001")).Returns("2015-01-01");
            _cardSetRepositoryMock.Setup(r => r.GetTCGDateBySetCode("NEW-EN002")).Returns("2020-01-01");
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CatalogPrintingID = 101 },
                new PreferredVersion { CatalogPrintingID = 201 }
            ]);

            var result = await _service.GetNewPrintingOpportunitiesAsync();

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("Alpha Card", result[0].CardName);
            Assert.AreEqual("Zeta Card", result[1].CardName);
        }

        [TestMethod]
        public async Task GetNewPrintingOpportunitiesAsync_NewerPrintingIsDismissed_ExcludesOpportunity()
        {
            SetUpDarkMagicianWithNewerPrinting();
            _dismissedNewPrintingRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new HashSet<int> { 102 });

            var result = await _service.GetNewPrintingOpportunitiesAsync();

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public async Task GetNewPrintingOpportunitiesAsync_NewerPrintingExists_IsIncludedWithItsPrintingID()
        {
            SetUpDarkMagicianWithNewerPrinting();

            var result = await _service.GetNewPrintingOpportunitiesAsync();

            var opportunity = result.Single();
            var newer = opportunity.NewerPrintings.Single();
            Assert.AreEqual(102, newer.PrintingID);
            Assert.AreEqual("NEW-EN001", newer.SetCode);
            Assert.AreEqual("LOB-EN001", opportunity.CurrentSetCode);
            Assert.IsFalse(opportunity.IsIgnored);
        }

        [TestMethod]
        public async Task GetNewPrintingOpportunitiesAsync_NewerPrintingRarityIsShortPrint_DisplaysAsCommon()
        {
            SetUpCard(1, "Dark Magician",
                new Set { Code = "LOB-EN001", PrintingID = 101, RarityName = "Ultra Rare" },
                new Set { Code = "NEW-EN001", PrintingID = 102, RarityName = "Short Print" });
            _cardSetRepositoryMock.Setup(r => r.GetTCGDateBySetCode("LOB-EN001")).Returns("2015-01-01");
            _cardSetRepositoryMock.Setup(r => r.GetTCGDateBySetCode("NEW-EN001")).Returns("2020-01-01");
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync([new PreferredVersion { CatalogPrintingID = 101 }]);

            var result = await _service.GetNewPrintingOpportunitiesAsync();

            Assert.AreEqual("Common", result.Single().NewerPrintings.Single().RarityName);
        }

        [TestMethod]
        public async Task GetNewPrintingOpportunitiesAsync_NewerSetHasTwoPrintVariants_BothSurfaceAndEachCanBeDismissed()
        {
            SetUpCard(1, "Dark Magical Curtain",
                new Set { Code = "LOB-EN001", PrintingID = 101, RarityName = "Ultra Rare" },
                new Set { Code = "RA05-EN001", PrintingID = 102, RarityName = "Ultra Rare", PrintVariant = "Extended Art" },
                new Set { Code = "RA05-EN001", PrintingID = 103, RarityName = "Ultra Rare", PrintVariant = "Starlight Rare" });
            _cardSetRepositoryMock.Setup(r => r.GetTCGDateBySetCode("LOB-EN001")).Returns("2015-01-01");
            _cardSetRepositoryMock.Setup(r => r.GetTCGDateBySetCode("RA05-EN001")).Returns("2020-01-01");
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync([new PreferredVersion { CatalogPrintingID = 101 }]);
            _dismissedNewPrintingRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new HashSet<int> { 102 });

            var result = await _service.GetNewPrintingOpportunitiesAsync();

            Assert.AreEqual("Starlight Rare", result.Single().NewerPrintings.Single().PrintVariant);
        }

        [TestMethod]
        public async Task GetNewPrintingOpportunitiesAsync_NoNewerPrintings_IsExcluded()
        {
            SetUpPrinting(101, "LOB-EN001", "Ultra Rare");
            _cardSetRepositoryMock.Setup(r => r.GetTCGDateBySetCode("LOB-EN001")).Returns("2015-01-01");
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync([new PreferredVersion { CatalogPrintingID = 101 }]);

            var result = await _service.GetNewPrintingOpportunitiesAsync();

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public async Task GetNewPrintingOpportunitiesAsync_TrackedPrintingLeftTheCatalog_IsSkipped()
        {
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync([new PreferredVersion { CatalogPrintingID = 999 }]);

            var result = await _service.GetNewPrintingOpportunitiesAsync();

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public async Task GetNewPrintingOpportunitiesAsync_TrackedVariantSameDateAsSibling_SiblingIsNotNewer()
        {
            SetUpCard(1, "Dark Magical Curtain",
                new Set { Code = "RA05-EN001", PrintingID = 101, RarityName = "Ultra Rare", PrintVariant = "Extended Art" },
                new Set { Code = "RA05-EN001", PrintingID = 102, RarityName = "Ultra Rare", PrintVariant = "Starlight Rare" });
            _cardSetRepositoryMock.Setup(r => r.GetTCGDateBySetCode("RA05-EN001")).Returns("2015-01-01");
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync([new PreferredVersion { CatalogPrintingID = 101 }]);

            var result = await _service.GetNewPrintingOpportunitiesAsync();

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public async Task GetNewPrintingOpportunitiesAsync_TrackedVariant_CurrentPrintVariantIsPopulated()
        {
            SetUpCard(1, "Dark Magical Curtain",
                new Set { Code = "RA05-EN001", PrintingID = 101, RarityName = "Ultra Rare", PrintVariant = "Extended Art" },
                new Set { Code = "RA06-EN001", PrintingID = 102, RarityName = "Ultra Rare", PrintVariant = "Extended Art" });
            _cardSetRepositoryMock.Setup(r => r.GetTCGDateBySetCode("RA05-EN001")).Returns("2015-01-01");
            _cardSetRepositoryMock.Setup(r => r.GetTCGDateBySetCode("RA06-EN001")).Returns("2020-01-01");
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync([new PreferredVersion { CatalogPrintingID = 101 }]);

            var result = await _service.GetNewPrintingOpportunitiesAsync();

            Assert.AreEqual("Extended Art", result.Single().CurrentPrintVariant);
        }

        private void SetUpDarkMagicianWithNewerPrinting()
        {
            SetUpCard(1, "Dark Magician",
                new Set { Code = "LOB-EN001", PrintingID = 101, RarityName = "Ultra Rare" },
                new Set { Code = "NEW-EN001", PrintingID = 102, RarityName = "Secret Rare" });
            _cardSetRepositoryMock.Setup(r => r.GetTCGDateBySetCode("LOB-EN001")).Returns("2015-01-01");
            _cardSetRepositoryMock.Setup(r => r.GetTCGDateBySetCode("NEW-EN001")).Returns("2020-01-01");
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync([new PreferredVersion { CatalogPrintingID = 101 }]);
        }
    }
}
