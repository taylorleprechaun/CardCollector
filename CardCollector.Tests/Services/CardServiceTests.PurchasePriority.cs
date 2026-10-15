using CardCollector.Data.Models;
using CardCollector.DTO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CardCollector.Tests.Services
{
    public partial class CardServiceTests
    {
        private static readonly DateTime AsOfUTC = new(2026, 7, 20);

        [TestMethod]
        public async Task GetPurchasePlanAsync_CandidateOverBudget_IsSkippedNotStopped()
        {
            SetUpFlaggableCard(1, 101, "LOB-EN001", "Secret Rare");
            SetUpFlaggableCard(2, 201, "LOB-EN002", "Ultra Rare");
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CardID = 1, CatalogPrintingID = 101 },
                new PreferredVersion { CardID = 2, CatalogPrintingID = 201 }
            ]);
            _pricingServiceMock.Setup(p => p.GetPrintingPriceAsync(101, CardEdition.FirstEdition)).ReturnsAsync(100.00m);
            _pricingServiceMock.Setup(p => p.GetPrintingPriceAsync(201, CardEdition.FirstEdition)).ReturnsAsync(5.00m);

            var plan = await _service.GetPurchasePlanAsync(totalBudget: 20.00m, asOfUTC: AsOfUTC);

            Assert.AreEqual(1, plan.Items.Count);
            Assert.AreEqual(15.00m, plan.TotalCost);
        }

        [TestMethod]
        public async Task GetPurchasePlanAsync_MaxCardsLimitsResultCount()
        {
            SetUpFlaggableCard(1, 101, "LOB-EN001", "Secret Rare");
            SetUpFlaggableCard(2, 201, "LOB-EN002", "Ultra Rare");
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CardID = 1, CatalogPrintingID = 101 },
                new PreferredVersion { CardID = 2, CatalogPrintingID = 201 }
            ]);
            _pricingServiceMock.Setup(p => p.GetPrintingPriceAsync(101, CardEdition.FirstEdition)).ReturnsAsync(5.00m);
            _pricingServiceMock.Setup(p => p.GetPrintingPriceAsync(201, CardEdition.FirstEdition)).ReturnsAsync(5.00m);

            var plan = await _service.GetPurchasePlanAsync(maxCards: 1, asOfUTC: AsOfUTC);

            Assert.AreEqual(1, plan.Items.Count);
        }

        [TestMethod]
        public async Task GetPurchasePlanAsync_NoBudgetOrCardLimit_IncludesAllCandidates()
        {
            SetUpFlaggableCard();
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CardID = 1, CatalogPrintingID = 101 }
            ]);
            _pricingServiceMock.Setup(p => p.GetPrintingPriceAsync(101, CardEdition.FirstEdition)).ReturnsAsync(5.00m);

            var plan = await _service.GetPurchasePlanAsync(asOfUTC: AsOfUTC);

            Assert.AreEqual(1, plan.Items.Count);
            Assert.AreEqual(15.00m, plan.TotalCost);
        }

        [TestMethod]
        public async Task GetPurchasePriorityCandidateAsync_LivePriceOverMaxPrice_ReturnsNull()
        {
            SetUpFlaggableCard();
            _pricingServiceMock.Setup(p => p.GetPrintingPriceAsync(101, CardEdition.FirstEdition)).ReturnsAsync(50.00m);

            var result = await _service.GetPurchasePriorityCandidateAsync(101, maxPrice: 10.00m, asOfUTC: AsOfUTC);

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetPurchasePriorityCandidateAsync_NoPriceDataButMaxPriceSpecified_ReturnsNull()
        {
            SetUpFlaggableCard();
            _pricingServiceMock.Setup(p => p.GetPrintingPriceAsync(101, CardEdition.FirstEdition)).ReturnsAsync((decimal?)null);

            var result = await _service.GetPurchasePriorityCandidateAsync(101, maxPrice: 10.00m, asOfUTC: AsOfUTC);

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetPurchasePriorityCandidateAsync_PreferredVersionAlreadyComplete_ReturnsNull()
        {
            SetUpFlaggableCard();
            _preferredVersionRepositoryMock.Setup(r => r.GetByCardIDAsync(1))
                .ReturnsAsync((IReadOnlyList<PreferredVersion>)[new PreferredVersion { CardID = 1, CatalogPrintingID = 101 }]);
            _collectionRepositoryMock.Setup(r => r.GetOwnedQuantitiesByPrintingAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync(new Dictionary<int, int> { [101] = 3 });

            var result = await _service.GetPurchasePriorityCandidateAsync(101, asOfUTC: AsOfUTC);

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetPurchasePriorityCandidateAsync_PrintingNotFound_ReturnsNull()
        {
            var result = await _service.GetPurchasePriorityCandidateAsync(101, asOfUTC: AsOfUTC);

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetPurchasePriorityCandidateAsync_QuantityNeededIsZero_ReturnsNull()
        {
            SetUpFlaggableCard();
            _collectionRepositoryMock.Setup(r => r.GetOwnedQuantitiesByPrintingAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync(new Dictionary<int, int> { [101] = 2 });
            _collectionRepositoryMock.Setup(r => r.GetOrderedQuantitiesAsync()).ReturnsAsync(new Dictionary<int, int> { [101] = 1 });

            var result = await _service.GetPurchasePriorityCandidateAsync(101, asOfUTC: AsOfUTC);

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetPurchasePriorityCandidateAsync_ValidFlaggableCandidate_ReturnsCandidateWithLivePrice()
        {
            SetUpFlaggableCard();
            _pricingServiceMock.Setup(p => p.GetPrintingPriceAsync(101, CardEdition.FirstEdition)).ReturnsAsync(9.99m);

            var result = await _service.GetPurchasePriorityCandidateAsync(101, asOfUTC: AsOfUTC);

            Assert.IsNotNull(result);
            Assert.AreEqual(101, result!.PrintingID);
            Assert.AreEqual(9.99m, result.Price);
            Assert.AreEqual(3, result.QuantityNeeded);
        }

        [TestMethod]
        public async Task GetPurchasePriorityCandidatesAsync_NoPreferredVersions_ReturnsEmpty()
        {
            var result = await _service.GetPurchasePriorityCandidatesAsync(AsOfUTC);

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public async Task GetPurchasePriorityCandidatesAsync_OneOfTwoTrackedPrintingsComplete_OnlyIncompleteOneIsReturned()
        {
            SetUpCard(1, "Dark Magician",
                new Set { Code = "LOB-EN001", PrintingID = 101, RarityName = "Secret Rare" },
                new Set { Code = "LOB-EN002", PrintingID = 102, RarityName = "Ultra Rare" });
            _cardSetRepositoryMock.Setup(r => r.GetTCGDateBySetCode(It.IsAny<string>())).Returns("2015-01-01");
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CardID = 1, CatalogPrintingID = 101, DesiredQuantity = 3 },
                new PreferredVersion { CardID = 1, CatalogPrintingID = 102, DesiredQuantity = 1 }
            ]);
            _collectionRepositoryMock.Setup(r => r.GetOwnedQuantitiesByPrintingAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync(new Dictionary<int, int> { [101] = 3 });
            _pricingServiceMock.Setup(p => p.GetPrintingPriceAsync(102, CardEdition.FirstEdition)).ReturnsAsync(9.99m);

            var result = await _service.GetPurchasePriorityCandidatesAsync(AsOfUTC);

            Assert.AreEqual("LOB-EN002", result.Single().SetCode);
        }

        [TestMethod]
        public async Task GetPurchasePriorityCandidatesAsync_OnlyTrackedPrintingComplete_ExcludesIt()
        {
            SetUpFlaggableCard();
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CardID = 1, CatalogPrintingID = 101 }
            ]);
            _collectionRepositoryMock.Setup(r => r.GetOwnedQuantitiesByPrintingAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync(new Dictionary<int, int> { [101] = 3 });

            var result = await _service.GetPurchasePriorityCandidatesAsync(AsOfUTC);

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public async Task GetPurchasePriorityCandidatesAsync_ValidCandidate_IsReturned()
        {
            SetUpFlaggableCard();
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CardID = 1, CatalogPrintingID = 101 }
            ]);
            _pricingServiceMock.Setup(p => p.GetPrintingPriceAsync(101, CardEdition.FirstEdition)).ReturnsAsync(9.99m);

            var result = await _service.GetPurchasePriorityCandidatesAsync(AsOfUTC);

            Assert.AreEqual("Dark Magician", result.Single().CardName);
        }

        private void SetUpFlaggableCard(int cardID = 1, int printingID = 101, string setCode = "LOB-EN001", string rarityName = "Secret Rare")
        {
            SetUpCard(cardID, "Dark Magician", new Set { Code = setCode, PrintingID = printingID, RarityName = rarityName });
            _cardSetRepositoryMock.Setup(r => r.GetTCGDateBySetCode(setCode)).Returns("2015-01-01");
        }
    }
}
