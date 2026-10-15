using CardCollector.Data.Models;
using CardCollector.DTO;
using CardCollector.Pages;
using CardCollector.Repository;
using CardCollector.Services;
using CardCollector.Tests.TestHelpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CardCollector.Tests.Pages
{
    [TestClass]
    public sealed class CardModelTests
    {
        private Mock<ICardService> _cardServiceMock = null!;
        private Mock<ICardSetRepository> _cardSetRepositoryMock = null!;

        [TestMethod]
        public async Task GetCompletionStatus_NotPreferredAndPreferredNotComplete_ReturnsPlaceholder()
        {
            _cardServiceMock.Setup(s => s.GetCardByID(1)).Returns(new Card { ID = 1 });
            _cardServiceMock.Setup(s => s.GetEntriesByCardIDAsync(1)).ReturnsAsync([]);
            _cardServiceMock.Setup(s => s.GetTrackedPrintingsByCardIDAsync(1))
                .ReturnsAsync((IReadOnlyList<PreferredVersion>)[new PreferredVersion { CatalogPrintingID = 101 }]);
            var page = CreatePage();
            page.ID = 1;
            await page.OnGetAsync();

            var result = page.GetCompletionStatus(CollectionStatus.Owned, 1, 102);

            Assert.AreEqual(CollectionCompletionStatus.Placeholder, result);
        }

        [TestMethod]
        public async Task GetCompletionStatus_NotPreferredButPreferredEntryIsCompleteElsewhere_ReturnsOwned()
        {
            _cardServiceMock.Setup(s => s.GetCardByID(1)).Returns(new Card { ID = 1 });
            _cardServiceMock.Setup(s => s.GetEntriesByCardIDAsync(1)).ReturnsAsync(
            [
                new CollectionEntry { CatalogPrintingID = 101, Status = CollectionStatus.Owned, Quantity = 3 }
            ]);
            _cardServiceMock.Setup(s => s.GetTrackedPrintingsByCardIDAsync(1))
                .ReturnsAsync((IReadOnlyList<PreferredVersion>)[new PreferredVersion { CatalogPrintingID = 101 }]);
            var page = CreatePage();
            page.ID = 1;
            await page.OnGetAsync();

            var result = page.GetCompletionStatus(CollectionStatus.Owned, 1, 102);

            Assert.AreEqual(CollectionCompletionStatus.Owned, result);
        }

        [TestMethod]
        public async Task GetCompletionStatus_PreferredAndAtThreshold_ReturnsComplete()
        {
            _cardServiceMock.Setup(s => s.GetCardByID(1)).Returns(new Card { ID = 1 });
            _cardServiceMock.Setup(s => s.GetEntriesByCardIDAsync(1)).ReturnsAsync([]);
            _cardServiceMock.Setup(s => s.GetTrackedPrintingsByCardIDAsync(1))
                .ReturnsAsync((IReadOnlyList<PreferredVersion>)[new PreferredVersion { CatalogPrintingID = 101 }]);
            var page = CreatePage();
            page.ID = 1;
            await page.OnGetAsync();

            var result = page.GetCompletionStatus(CollectionStatus.Owned, 3, 101);

            Assert.AreEqual(CollectionCompletionStatus.Complete, result);
        }

        [TestMethod]
        public async Task GetCompletionStatus_PreferredAtCustomDesiredQuantity_ReturnsComplete()
        {
            _cardServiceMock.Setup(s => s.GetCardByID(1)).Returns(new Card { ID = 1 });
            _cardServiceMock.Setup(s => s.GetEntriesByCardIDAsync(1)).ReturnsAsync([]);
            _cardServiceMock.Setup(s => s.GetTrackedPrintingsByCardIDAsync(1))
                .ReturnsAsync((IReadOnlyList<PreferredVersion>)[new PreferredVersion { CatalogPrintingID = 101, DesiredQuantity = 1 }]);
            var page = CreatePage();
            page.ID = 1;
            await page.OnGetAsync();

            var result = page.GetCompletionStatus(CollectionStatus.Owned, 1, 101);

            Assert.AreEqual(CollectionCompletionStatus.Complete, result);
        }

        [TestMethod]
        public async Task GetCompletionStatus_PreferredBelowThreshold_ReturnsIncomplete()
        {
            _cardServiceMock.Setup(s => s.GetCardByID(1)).Returns(new Card { ID = 1 });
            _cardServiceMock.Setup(s => s.GetEntriesByCardIDAsync(1)).ReturnsAsync([]);
            _cardServiceMock.Setup(s => s.GetTrackedPrintingsByCardIDAsync(1))
                .ReturnsAsync((IReadOnlyList<PreferredVersion>)[new PreferredVersion { CatalogPrintingID = 101 }]);
            var page = CreatePage();
            page.ID = 1;
            await page.OnGetAsync();

            var result = page.GetCompletionStatus(CollectionStatus.Owned, 1, 101);

            Assert.AreEqual(CollectionCompletionStatus.Incomplete, result);
        }

        [TestMethod]
        public void GetCompletionStatus_StatusIsOrdered_ReturnsNull()
        {
            var page = CreatePage();

            var result = page.GetCompletionStatus(CollectionStatus.Ordered, 3, 101);

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetCompletionStatus_TwoTrackedPrintingsOnlySecondMatches_ReturnsThatOnesStatus()
        {
            _cardServiceMock.Setup(s => s.GetCardByID(1)).Returns(new Card { ID = 1 });
            _cardServiceMock.Setup(s => s.GetEntriesByCardIDAsync(1)).ReturnsAsync([]);
            _cardServiceMock.Setup(s => s.GetTrackedPrintingsByCardIDAsync(1))
                .ReturnsAsync((IReadOnlyList<PreferredVersion>)[
                    new PreferredVersion { CatalogPrintingID = 201, DesiredQuantity = 3 },
                    new PreferredVersion { CatalogPrintingID = 202, DesiredQuantity = 1 }
                ]);
            var page = CreatePage();
            page.ID = 1;
            await page.OnGetAsync();

            var result = page.GetCompletionStatus(CollectionStatus.Owned, 1, 202);

            Assert.AreEqual(CollectionCompletionStatus.Complete, result);
        }

        [TestMethod]
        public void GetTCGDate_DelegatesToCardSetRepository()
        {
            _cardSetRepositoryMock.Setup(r => r.GetTCGDateBySetCode("LOB-EN001")).Returns("2002-03-08");
            var page = CreatePage();

            Assert.AreEqual("2002-03-08", page.GetTCGDate("LOB-EN001"));
        }

        [TestMethod]
        public async Task OnGetAsync_CardNotFoundInData_SetsCardNotFound()
        {
            _cardServiceMock.Setup(s => s.GetCardByID(1)).Returns((Card?)null);
            var page = CreatePage();
            page.ID = 1;

            await page.OnGetAsync();

            Assert.IsTrue(page.CardNotFound);
        }

        [TestMethod]
        public async Task OnGetAsync_IDIsZero_SetsCardNotFound()
        {
            var page = CreatePage();
            page.ID = 0;

            await page.OnGetAsync();

            Assert.IsTrue(page.CardNotFound);
        }

        [TestMethod]
        public async Task OnGetAsync_EntriesOnTwoPrintingsOfOneSetCode_SummarizesEachPrinting()
        {
            _cardServiceMock.Setup(s => s.GetCardByID(1)).Returns(new Card { ID = 1 });
            _cardServiceMock.Setup(s => s.GetEntriesByCardIDAsync(1)).ReturnsAsync(
            [
                new CollectionEntry { CatalogPrintingID = 301, Status = CollectionStatus.Owned, Quantity = 1 },
                new CollectionEntry { CatalogPrintingID = 302, Status = CollectionStatus.Owned, Quantity = 2 }
            ]);
            var page = CreatePage();
            page.ID = 1;

            await page.OnGetAsync();

            Assert.AreEqual(1, page.CollectionByPrinting[301].TotalQuantity);
            Assert.AreEqual(2, page.CollectionByPrinting[302].TotalQuantity);
        }

        [TestMethod]
        public async Task OnGetAsync_MultipleEntriesInSameGroup_OwnedBeatsOrderedAndSumsQuantity()
        {
            _cardServiceMock.Setup(s => s.GetCardByID(1)).Returns(new Card { ID = 1 });
            _cardServiceMock.Setup(s => s.GetEntriesByCardIDAsync(1)).ReturnsAsync(
            [
                new CollectionEntry { CatalogPrintingID = 101, Status = CollectionStatus.Ordered, Quantity = 1 },
                new CollectionEntry { CatalogPrintingID = 101, Status = CollectionStatus.Owned, Quantity = 2 }
            ]);
            var page = CreatePage();
            page.ID = 1;

            await page.OnGetAsync();

            var summary = page.CollectionByPrinting[101];
            Assert.AreEqual(CollectionStatus.Owned, summary.Status);
            Assert.AreEqual(3, summary.TotalQuantity);
        }

        [TestMethod]
        public async Task OnGetAsync_ValidCard_PopulatesTrackedPrintingsAndIsIgnored()
        {
            _cardServiceMock.Setup(s => s.GetCardByID(1)).Returns(new Card { ID = 1, Name = "Dark Magician" });
            _cardServiceMock.Setup(s => s.GetEntriesByCardIDAsync(1)).ReturnsAsync([]);
            var pv = new PreferredVersion { CatalogPrintingID = 101 };
            _cardServiceMock.Setup(s => s.GetTrackedPrintingsByCardIDAsync(1)).ReturnsAsync((IReadOnlyList<PreferredVersion>)[pv]);
            _cardServiceMock.Setup(s => s.IsCardIgnoredAsync(1)).ReturnsAsync(true);
            var page = CreatePage();
            page.ID = 1;

            await page.OnGetAsync();

            Assert.IsFalse(page.CardNotFound);
            Assert.AreEqual(1, page.TrackedPrintings.Count);
            Assert.AreSame(pv, page.TrackedPrintings[0]);
            Assert.IsTrue(page.IsIgnored);
        }

        [TestMethod]
        public async Task OnPostIgnoreAsync_IgnoresCardAndRedirects()
        {
            var page = CreatePage();
            page.CardID = 5;

            var result = await page.OnPostIgnoreAsync();

            _cardServiceMock.Verify(s => s.IgnoreCardAsync(5), Times.Once);
            Assert.IsInstanceOfType<RedirectToPageResult>(result);
        }

        [TestMethod]
        public async Task OnPostOrderAsync_AddsOrderedEntryAndRedirects()
        {
            var page = CreatePage();
            page.CardID = 1;
            page.PrintingID = 101;

            var result = await page.OnPostOrderAsync(quantity: 2, edition: null);

            _cardServiceMock.Verify(s => s.AddEntryAsync(
                101, CollectionStatus.Ordered, 2,
                null, null, null, null, null, null), Times.Once);
            Assert.IsInstanceOfType<RedirectToPageResult>(result);
        }

        [TestMethod]
        public async Task OnPostOrderAsync_SetAsPreferredTrue_AlsoSavesPreferredVersion()
        {
            var page = CreatePage();
            page.CardID = 1;
            page.PrintingID = 101;

            await page.OnPostOrderAsync(setAsPreferred: true);

            _cardServiceMock.Verify(s => s.SavePreferredVersionAsync(101, null), Times.Once);
        }

        [TestMethod]
        public async Task OnPostOwnAsync_AddsOwnedEntryAndRedirects()
        {
            var page = CreatePage();
            page.CardID = 1;
            page.PrintingID = 101;

            var result = await page.OnPostOwnAsync(quantity: 3);

            _cardServiceMock.Verify(s => s.AddEntryAsync(
                101, CollectionStatus.Owned, 3,
                null, null, null, null, null, null), Times.Once);
            Assert.IsInstanceOfType<RedirectToPageResult>(result);
        }

        [TestMethod]
        public async Task OnPostOwnAsync_SetAsPreferredTrue_AlsoSavesPreferredVersion()
        {
            var page = CreatePage();
            page.CardID = 1;
            page.PrintingID = 101;

            await page.OnPostOwnAsync(quantity: 3, setAsPreferred: true);

            _cardServiceMock.Verify(s => s.SavePreferredVersionAsync(101, null), Times.Once);
        }

        [TestMethod]
        public async Task OnPostRemovePreferredAsync_RemovesFromWishlistAndRedirects()
        {
            var page = CreatePage();

            var result = await page.OnPostRemovePreferredAsync(10);

            _cardServiceMock.Verify(s => s.RemoveFromWishlistAsync(10), Times.Once);
            Assert.IsInstanceOfType<RedirectToPageResult>(result);
        }

        [TestMethod]
        public async Task OnPostSetDesiredQuantityAsync_InvalidDesiredQuantity_DoesNotCallServiceButStillRedirects()
        {
            var page = CreatePage();

            var result = await page.OnPostSetDesiredQuantityAsync(10, 0);

            _cardServiceMock.Verify(s => s.SetDesiredQuantityAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
            Assert.IsInstanceOfType<RedirectToPageResult>(result);
        }

        [TestMethod]
        public async Task OnPostSetDesiredQuantityAsync_InvalidPreferredVersionID_DoesNotCallServiceButStillRedirects()
        {
            var page = CreatePage();

            var result = await page.OnPostSetDesiredQuantityAsync(0, 1);

            _cardServiceMock.Verify(s => s.SetDesiredQuantityAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
            Assert.IsInstanceOfType<RedirectToPageResult>(result);
        }

        [TestMethod]
        public async Task OnPostSetDesiredQuantityAsync_ValidParams_UpdatesAndRedirects()
        {
            var page = CreatePage();

            var result = await page.OnPostSetDesiredQuantityAsync(10, 1);

            _cardServiceMock.Verify(s => s.SetDesiredQuantityAsync(10, 1), Times.Once);
            Assert.IsInstanceOfType<RedirectToPageResult>(result);
        }

        [TestMethod]
        public async Task OnPostSetPreferredAsync_SavesPreferredVersionAndRedirects()
        {
            var page = CreatePage();
            page.CardID = 1;
            page.PrintingID = 101;

            var result = await page.OnPostSetPreferredAsync();

            _cardServiceMock.Verify(s => s.SavePreferredVersionAsync(101, null), Times.Once);
            Assert.IsInstanceOfType<RedirectToPageResult>(result);
        }

        [TestMethod]
        public async Task OnPostUnignoreAsync_UnignoresCardAndRedirects()
        {
            var page = CreatePage();
            page.CardID = 5;

            var result = await page.OnPostUnignoreAsync();

            _cardServiceMock.Verify(s => s.UnignoreCardAsync(5), Times.Once);
            Assert.IsInstanceOfType<RedirectToPageResult>(result);
        }

        [TestInitialize]
        public void Setup()
        {
            _cardServiceMock = new Mock<ICardService>();
            _cardSetRepositoryMock = new Mock<ICardSetRepository>();
        }

        private CardModel CreatePage()
        {
            var page = new CardModel(_cardServiceMock.Object, _cardSetRepositoryMock.Object);
            PageContextFactory.Attach(page);
            return page;
        }
    }
}
