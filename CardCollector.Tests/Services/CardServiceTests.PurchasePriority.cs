using CardCollector.Data.Models;
using CardCollector.DTO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CardCollector.Tests.Services
{
    public partial class CardServiceTests
    {
        private static readonly DateTime AsOfUtc = new(2026, 7, 20);

        [TestMethod]
        public async Task GetPurchasePlanAsync_CandidateOverBudget_IsSkippedNotStopped()
        {
            SetUpFlaggableCard(1, "LOB-EN001", "Secret Rare");
            SetUpFlaggableCard(2, "LOB-EN002", "Ultra Rare");
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = "Secret Rare" },
                new PreferredVersion { CardID = 2, ImageID = 20, SetCode = "LOB-EN002", RarityName = "Ultra Rare" }
            ]);
            _pricingServiceMock
                .Setup(p => p.GetPrintingPriceAsync(1, "LOB-EN001", "Secret Rare", CardEdition.FirstEdition))
                .ReturnsAsync(100.00m);
            _pricingServiceMock
                .Setup(p => p.GetPrintingPriceAsync(2, "LOB-EN002", "Ultra Rare", CardEdition.FirstEdition))
                .ReturnsAsync(5.00m);

            var plan = await _service.GetPurchasePlanAsync(totalBudget: 20.00m, asOfUtc: AsOfUtc);

            Assert.AreEqual(1, plan.Items.Count);
            Assert.AreEqual(15.00m, plan.TotalCost);
        }

        [TestMethod]
        public async Task GetPurchasePlanAsync_MaxCardsLimitsResultCount()
        {
            SetUpFlaggableCard(1, "LOB-EN001", "Secret Rare");
            SetUpFlaggableCard(2, "LOB-EN002", "Ultra Rare");
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = "Secret Rare" },
                new PreferredVersion { CardID = 2, ImageID = 20, SetCode = "LOB-EN002", RarityName = "Ultra Rare" }
            ]);
            _pricingServiceMock.Setup(p => p.GetPrintingPriceAsync(1, "LOB-EN001", "Secret Rare", CardEdition.FirstEdition)).ReturnsAsync(5.00m);
            _pricingServiceMock.Setup(p => p.GetPrintingPriceAsync(2, "LOB-EN002", "Ultra Rare", CardEdition.FirstEdition)).ReturnsAsync(5.00m);

            var plan = await _service.GetPurchasePlanAsync(maxCards: 1, asOfUtc: AsOfUtc);

            Assert.AreEqual(1, plan.Items.Count);
        }

        [TestMethod]
        public async Task GetPurchasePlanAsync_NoBudgetOrCardLimit_IncludesAllCandidates()
        {
            SetUpFlaggableCard(1, "LOB-EN001", "Secret Rare");
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = "Secret Rare" }
            ]);
            _pricingServiceMock.Setup(p => p.GetPrintingPriceAsync(1, "LOB-EN001", "Secret Rare", CardEdition.FirstEdition)).ReturnsAsync(5.00m);

            var plan = await _service.GetPurchasePlanAsync(asOfUtc: AsOfUtc);

            Assert.AreEqual(1, plan.Items.Count);
            Assert.AreEqual(15.00m, plan.TotalCost);
        }

        [TestMethod]
        public async Task GetPurchasePriorityCandidateAsync_CardNotFound_ReturnsNull()
        {
            var result = await _service.GetPurchasePriorityCandidateAsync(1, 10, "LOB-EN001", "Secret Rare", asOfUtc: AsOfUtc);

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetPurchasePriorityCandidateAsync_LivePriceOverMaxPrice_ReturnsNull()
        {
            SetUpFlaggableCard();
            _pricingServiceMock
                .Setup(p => p.GetPrintingPriceAsync(1, "LOB-EN001", "Secret Rare", CardEdition.FirstEdition))
                .ReturnsAsync(50.00m);

            var result = await _service.GetPurchasePriorityCandidateAsync(1, 10, "LOB-EN001", "Secret Rare", maxPrice: 10.00m, asOfUtc: AsOfUtc);

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetPurchasePriorityCandidateAsync_NoPriceDataButMaxPriceSpecified_ReturnsNull()
        {
            SetUpFlaggableCard();
            _pricingServiceMock
                .Setup(p => p.GetPrintingPriceAsync(1, "LOB-EN001", "Secret Rare", CardEdition.FirstEdition))
                .ReturnsAsync((decimal?)null);

            var result = await _service.GetPurchasePriorityCandidateAsync(1, 10, "LOB-EN001", "Secret Rare", maxPrice: 10.00m, asOfUtc: AsOfUtc);

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetPurchasePriorityCandidateAsync_PreferredVersionAlreadyComplete_ReturnsNull()
        {
            SetUpFlaggableCard();
            _preferredVersionRepositoryMock.Setup(r => r.GetByCardIDAsync(1))
                .ReturnsAsync(new PreferredVersion { CardID = 1, ImageID = 10, SetCode = "LOB-EN001" });
            _collectionRepositoryMock
                .Setup(r => r.GetOwnedQuantitiesForPreferredVersionsAsync(It.IsAny<IEnumerable<(int, string, string?)>>()))
                .ReturnsAsync(new Dictionary<(int, string), int> { [(10, "LOB-EN001")] = 3 });

            var result = await _service.GetPurchasePriorityCandidateAsync(1, 10, "LOB-EN001", "Secret Rare", asOfUtc: AsOfUtc);

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetPurchasePriorityCandidateAsync_QuantityNeededIsZero_ReturnsNull()
        {
            SetUpFlaggableCard();
            _collectionRepositoryMock
                .Setup(r => r.GetOwnedQuantitiesForPreferredVersionsAsync(It.IsAny<IEnumerable<(int, string, string?)>>()))
                .ReturnsAsync(new Dictionary<(int, string), int> { [(10, "LOB-EN001")] = 2 });
            _collectionRepositoryMock.Setup(r => r.GetOrderedQuantitiesAsync())
                .ReturnsAsync(new Dictionary<(int, string, string), int> { [(10, "LOB-EN001", "Secret Rare")] = 1 });

            var result = await _service.GetPurchasePriorityCandidateAsync(1, 10, "LOB-EN001", "Secret Rare", asOfUtc: AsOfUtc);

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetPurchasePriorityCandidateAsync_ValidFlaggableCandidate_ReturnsCandidateWithLivePrice()
        {
            SetUpFlaggableCard();
            _pricingServiceMock
                .Setup(p => p.GetPrintingPriceAsync(1, "LOB-EN001", "Secret Rare", CardEdition.FirstEdition))
                .ReturnsAsync(9.99m);

            var result = await _service.GetPurchasePriorityCandidateAsync(1, 10, "LOB-EN001", "Secret Rare", asOfUtc: AsOfUtc);

            Assert.IsNotNull(result);
            Assert.AreEqual(9.99m, result!.Price);
            Assert.AreEqual(3, result.QuantityNeeded);
        }

        [TestMethod]
        public async Task GetPurchasePriorityCandidatesAsync_CardWithAnyPreferredComplete_ExcludesEntireCard()
        {
            SetUpFlaggableCard();
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = "Secret Rare" }
            ]);
            _collectionRepositoryMock
                .Setup(r => r.GetOwnedQuantitiesForPreferredVersionsAsync(It.IsAny<IEnumerable<(int, string, string?)>>()))
                .ReturnsAsync(new Dictionary<(int, string), int> { [(10, "LOB-EN001")] = 3 });

            var result = await _service.GetPurchasePriorityCandidatesAsync(AsOfUtc);

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public async Task GetPurchasePriorityCandidatesAsync_NoPreferredVersions_ReturnsEmpty()
        {
            var result = await _service.GetPurchasePriorityCandidatesAsync(AsOfUtc);

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public async Task GetPurchasePriorityCandidatesAsync_ValidCandidate_IsReturned()
        {
            SetUpFlaggableCard();
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = "Secret Rare" }
            ]);
            _pricingServiceMock
                .Setup(p => p.GetPrintingPriceAsync(1, "LOB-EN001", "Secret Rare", CardEdition.FirstEdition))
                .ReturnsAsync(9.99m);

            var result = await _service.GetPurchasePriorityCandidatesAsync(AsOfUtc);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("Dark Magician", result[0].CardName);
        }

        private void SetUpFlaggableCard(int cardID = 1, string setCode = "LOB-EN001", string rarityName = "Secret Rare")
        {
            _cardDataRepositoryMock.Setup(r => r.GetCardByID(cardID)).Returns(new Card
            {
                ID = cardID,
                Name = "Dark Magician",
                CardSets = [new Set { Code = setCode, RarityName = rarityName }]
            });
            _cardSetRepositoryMock.Setup(r => r.GetTCGDateBySetCode(setCode)).Returns("2015-01-01");
        }
    }
}
