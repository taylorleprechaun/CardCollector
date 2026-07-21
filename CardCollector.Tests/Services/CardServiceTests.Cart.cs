using CardCollector.Data.Models;
using CardCollector.DTO;
using CardCollector.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CardCollector.Tests.Services
{
    public partial class CardServiceTests
    {
        [TestMethod]
        public async Task SubmitCartAsync_DeletesSubmittedLinesFromCart()
        {
            _pendingOrderRepositoryMock.Setup(r => r.GetByIDsAsync(It.IsAny<IEnumerable<int>>())).ReturnsAsync(
            [
                new PendingOrderLine { ID = 1, CardID = 1, ImageID = 10, SetCode = "LOB-EN001", Quantity = 1 },
                new PendingOrderLine { ID = 2, CardID = 1, ImageID = 10, SetCode = "LOB-EN001", Quantity = 1 }
            ]);

            await _service.SubmitCartAsync([]);

            _pendingOrderRepositoryMock.Verify(r => r.DeleteRangeAsync(
                It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { 1, 2 }))), Times.Once);
        }

        [TestMethod]
        public async Task SubmitCartAsync_EditionMatchesLiveData_NoWarning()
        {
            _pendingOrderRepositoryMock.Setup(r => r.GetByIDsAsync(It.IsAny<IEnumerable<int>>())).ReturnsAsync(
            [
                new PendingOrderLine { ID = 1, CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = "Ultra Rare", Edition = CardEdition.FirstEdition, Quantity = 1 }
            ]);
            _pricingServiceMock.Setup(p => p.GetCardEditionMapAsync(1)).ReturnsAsync(
                new Dictionary<(string SetCode, string RarityName), IReadOnlySet<CardEdition>>
                {
                    [("LOB-EN001", "ULTRA RARE")] = new HashSet<CardEdition> { CardEdition.FirstEdition }
                });

            var (_, _, warnings) = await _service.SubmitCartAsync([]);

            Assert.AreEqual(0, warnings.Count);
        }

        [TestMethod]
        public async Task SubmitCartAsync_EditionMismatchDetected_AddsWarningMessage()
        {
            _pendingOrderRepositoryMock.Setup(r => r.GetByIDsAsync(It.IsAny<IEnumerable<int>>())).ReturnsAsync(
            [
                new PendingOrderLine { ID = 1, CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = "Ultra Rare", Edition = CardEdition.FirstEdition, Quantity = 1 }
            ]);
            _pricingServiceMock.Setup(p => p.GetCardEditionMapAsync(1)).ReturnsAsync(
                new Dictionary<(string SetCode, string RarityName), IReadOnlySet<CardEdition>>
                {
                    [("LOB-EN001", "ULTRA RARE")] = new HashSet<CardEdition> { CardEdition.Unlimited }
                });

            var (_, _, warnings) = await _service.SubmitCartAsync([]);

            Assert.AreEqual(1, warnings.Count);
            StringAssert.Contains(warnings[0], "1st Edition");
        }

        [TestMethod]
        public async Task SubmitCartAsync_NoEditionRecorded_SkipsEditionCheckEntirely()
        {
            _pendingOrderRepositoryMock.Setup(r => r.GetByIDsAsync(It.IsAny<IEnumerable<int>>())).ReturnsAsync(
            [
                new PendingOrderLine { ID = 1, CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = "Ultra Rare", Edition = null, Quantity = 1 }
            ]);

            var (_, _, warnings) = await _service.SubmitCartAsync([]);

            Assert.AreEqual(0, warnings.Count);
            _pricingServiceMock.Verify(p => p.GetCardEditionMapAsync(It.IsAny<int>()), Times.Never);
        }

        [TestMethod]
        public async Task SubmitCartAsync_NoOverrideForLine_UsesLinesOwnStoredValues()
        {
            _pendingOrderRepositoryMock.Setup(r => r.GetByIDsAsync(It.IsAny<IEnumerable<int>>())).ReturnsAsync(
            [
                new PendingOrderLine { ID = 1, CardID = 1, ImageID = 10, SetCode = "LOB-EN001", Quantity = 2, PurchasePrice = 5.00m }
            ]);

            var (count, total, warnings) = await _service.SubmitCartAsync([new CartLineOverride { PendingOrderLineID = 999 }]);

            Assert.AreEqual(1, count);
            Assert.AreEqual(10.00m, total);
            Assert.AreEqual(0, warnings.Count);
            _collectionRepositoryMock.Verify(r => r.AddAsync(It.Is<CollectionEntry>(e => e.Quantity == 2 && e.PurchasePrice == 5.00m)), Times.Once);
        }

        [TestMethod]
        public async Task SubmitCartAsync_NoPendingLines_ReturnsZeroCountAndTotal()
        {
            _pendingOrderRepositoryMock.Setup(r => r.GetByIDsAsync(It.IsAny<IEnumerable<int>>())).ReturnsAsync([]);

            var (count, total, warnings) = await _service.SubmitCartAsync([]);

            Assert.AreEqual(0, count);
            Assert.AreEqual(0m, total);
            Assert.AreEqual(0, warnings.Count);
        }

        [TestMethod]
        public async Task SubmitCartAsync_NullOverrides_ThrowsArgumentNullException()
        {
            await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => _service.SubmitCartAsync(null!));
        }
        [TestMethod]
        public async Task SubmitCartAsync_OverrideProvided_UsesOverrideValuesWithClampedQuantity()
        {
            _pendingOrderRepositoryMock.Setup(r => r.GetByIDsAsync(It.IsAny<IEnumerable<int>>())).ReturnsAsync(
            [
                new PendingOrderLine { ID = 1, CardID = 1, ImageID = 10, SetCode = "LOB-EN001", Quantity = 1, PurchasePrice = 5.00m }
            ]);

            var (_, total, _) = await _service.SubmitCartAsync(
            [
                new CartLineOverride { PendingOrderLineID = 1, Quantity = 10, PurchasePrice = 20.00m }
            ]);

            Assert.AreEqual(60.00m, total); // clamped to MaxCartQuantity (3) * 20.00
            _collectionRepositoryMock.Verify(r => r.AddAsync(It.Is<CollectionEntry>(e => e.Quantity == 3 && e.PurchasePrice == 20.00m)), Times.Once);
        }
    }
}
