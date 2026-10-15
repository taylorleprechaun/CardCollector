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
            SetUpPrinting(101, "LOB-EN001", "Ultra Rare");
            _pendingOrderRepositoryMock.Setup(r => r.GetByIDsAsync(It.IsAny<IEnumerable<int>>())).ReturnsAsync(
            [
                new PendingOrderLine { ID = 1, CardID = 1, CatalogPrintingID = 101, Quantity = 1 },
                new PendingOrderLine { ID = 2, CardID = 1, CatalogPrintingID = 101, Quantity = 1 }
            ]);

            await _service.SubmitCartAsync([]);

            _pendingOrderRepositoryMock.Verify(r => r.DeleteRangeAsync(
                It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { 1, 2 }))), Times.Once);
        }

        [TestMethod]
        public async Task SubmitCartAsync_EditionListedForPrinting_NoWarning()
        {
            SetUpPrinting(101, "LOB-EN001", "Ultra Rare");
            _pendingOrderRepositoryMock.Setup(r => r.GetByIDsAsync(It.IsAny<IEnumerable<int>>())).ReturnsAsync(
            [
                new PendingOrderLine { ID = 1, CardID = 1, CatalogPrintingID = 101, Edition = CardEdition.FirstEdition, Quantity = 1 }
            ]);
            _pricingServiceMock.Setup(p => p.GetEditionsByPrintingAsync(1))
                .ReturnsAsync(new Dictionary<int, IReadOnlySet<CardEdition>> { [101] = new HashSet<CardEdition> { CardEdition.FirstEdition } });

            var (_, _, warnings) = await _service.SubmitCartAsync([]);

            Assert.AreEqual(0, warnings.Count);
        }

        [TestMethod]
        public async Task SubmitCartAsync_EditionMismatchDetected_AddsWarningNamingThePrinting()
        {
            SetUpPrinting(101, "LOB-EN001", "Ultra Rare");
            _pendingOrderRepositoryMock.Setup(r => r.GetByIDsAsync(It.IsAny<IEnumerable<int>>())).ReturnsAsync(
            [
                new PendingOrderLine { ID = 1, CardID = 1, CatalogPrintingID = 101, Edition = CardEdition.FirstEdition, Quantity = 1 }
            ]);
            _pricingServiceMock.Setup(p => p.GetEditionsByPrintingAsync(1))
                .ReturnsAsync(new Dictionary<int, IReadOnlySet<CardEdition>> { [101] = new HashSet<CardEdition> { CardEdition.Unlimited } });

            var (_, _, warnings) = await _service.SubmitCartAsync([]);

            StringAssert.Contains(warnings.Single(), "1st Edition printing of LOB-EN001 Ultra Rare");
        }

        [TestMethod]
        public async Task SubmitCartAsync_LineWhosePrintingLeftTheCatalog_StaysInCartWithWarning()
        {
            SetUpPrinting(101, "LOB-EN001", "Ultra Rare");
            _pendingOrderRepositoryMock.Setup(r => r.GetByIDsAsync(It.IsAny<IEnumerable<int>>())).ReturnsAsync(
            [
                new PendingOrderLine { ID = 1, CardID = 1, CatalogPrintingID = 101, Quantity = 1 },
                new PendingOrderLine { ID = 2, CardID = 1, CatalogPrintingID = 999, Quantity = 1 }
            ]);

            var (count, _, warnings) = await _service.SubmitCartAsync([]);

            Assert.AreEqual(1, count);
            Assert.AreEqual(1, warnings.Count);
            _pendingOrderRepositoryMock.Verify(r => r.DeleteRangeAsync(
                It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { 1 }))), Times.Once);
        }

        [TestMethod]
        public async Task SubmitCartAsync_NoEditionRecorded_SkipsEditionCheckEntirely()
        {
            SetUpPrinting(101, "LOB-EN001", "Ultra Rare");
            _pendingOrderRepositoryMock.Setup(r => r.GetByIDsAsync(It.IsAny<IEnumerable<int>>())).ReturnsAsync(
            [
                new PendingOrderLine { ID = 1, CardID = 1, CatalogPrintingID = 101, Edition = null, Quantity = 1 }
            ]);

            var (_, _, warnings) = await _service.SubmitCartAsync([]);

            Assert.AreEqual(0, warnings.Count);
            _pricingServiceMock.Verify(p => p.GetEditionsByPrintingAsync(It.IsAny<int>()), Times.Never);
        }

        [TestMethod]
        public async Task SubmitCartAsync_NoOverrideForLine_UsesLinesOwnStoredValues()
        {
            SetUpPrinting(101, "LOB-EN001", "Ultra Rare");
            _pendingOrderRepositoryMock.Setup(r => r.GetByIDsAsync(It.IsAny<IEnumerable<int>>())).ReturnsAsync(
            [
                new PendingOrderLine { ID = 1, CardID = 1, CatalogPrintingID = 101, Quantity = 2, PurchasePrice = 5.00m }
            ]);

            var (count, total, warnings) = await _service.SubmitCartAsync([new CartLineOverride { PendingOrderLineID = 999 }]);

            Assert.AreEqual(1, count);
            Assert.AreEqual(10.00m, total);
            Assert.AreEqual(0, warnings.Count);
            _collectionRepositoryMock.Verify(r => r.AddAsync(It.Is<CollectionEntry>(e =>
                e.CatalogPrintingID == 101 && e.Status == CollectionStatus.Ordered && e.Quantity == 2 && e.PurchasePrice == 5.00m)), Times.Once);
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
            SetUpPrinting(101, "LOB-EN001", "Ultra Rare");
            _pendingOrderRepositoryMock.Setup(r => r.GetByIDsAsync(It.IsAny<IEnumerable<int>>())).ReturnsAsync(
            [
                new PendingOrderLine { ID = 1, CardID = 1, CatalogPrintingID = 101, Quantity = 1, PurchasePrice = 5.00m }
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
