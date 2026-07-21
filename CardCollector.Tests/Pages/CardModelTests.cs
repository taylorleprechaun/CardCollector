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
            _cardServiceMock.Setup(s => s.GetPreferredVersionByCardIDAsync(1))
                .ReturnsAsync(new PreferredVersion { CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = "Ultra Rare" });
            var page = CreatePage();
            page.ID = 1;
            await page.OnGetAsync();

            var result = page.GetCompletionStatus(CollectionStatus.Owned, 1, "LOB-EN002", "Secret Rare");

            Assert.AreEqual(CollectionCompletionStatus.Placeholder, result);
        }

        [TestMethod]
        public async Task GetCompletionStatus_NotPreferredButPreferredEntryIsCompleteElsewhere_ReturnsOwned()
        {
            _cardServiceMock.Setup(s => s.GetCardByID(1)).Returns(new Card { ID = 1 });
            _cardServiceMock.Setup(s => s.GetEntriesByCardIDAsync(1)).ReturnsAsync(
            [
                new CollectionEntry { CardID = 1, SetCode = "LOB-EN001", RarityName = "Ultra Rare", Status = CollectionStatus.Owned, Quantity = 3 }
            ]);
            _cardServiceMock.Setup(s => s.GetPreferredVersionByCardIDAsync(1))
                .ReturnsAsync(new PreferredVersion { CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = "Ultra Rare" });
            var page = CreatePage();
            page.ID = 1;
            await page.OnGetAsync();

            var result = page.GetCompletionStatus(CollectionStatus.Owned, 1, "LOB-EN002", "Secret Rare");

            Assert.AreEqual(CollectionCompletionStatus.Owned, result);
        }

        [TestMethod]
        public async Task GetCompletionStatus_PreferredAndAtThreshold_ReturnsComplete()
        {
            _cardServiceMock.Setup(s => s.GetCardByID(1)).Returns(new Card { ID = 1 });
            _cardServiceMock.Setup(s => s.GetEntriesByCardIDAsync(1)).ReturnsAsync([]);
            _cardServiceMock.Setup(s => s.GetPreferredVersionByCardIDAsync(1))
                .ReturnsAsync(new PreferredVersion { CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = "Ultra Rare" });
            var page = CreatePage();
            page.ID = 1;
            await page.OnGetAsync();

            var result = page.GetCompletionStatus(CollectionStatus.Owned, 3, "LOB-EN001", "Ultra Rare");

            Assert.AreEqual(CollectionCompletionStatus.Complete, result);
        }

        [TestMethod]
        public async Task GetCompletionStatus_PreferredBelowThreshold_ReturnsIncomplete()
        {
            _cardServiceMock.Setup(s => s.GetCardByID(1)).Returns(new Card { ID = 1 });
            _cardServiceMock.Setup(s => s.GetEntriesByCardIDAsync(1)).ReturnsAsync([]);
            _cardServiceMock.Setup(s => s.GetPreferredVersionByCardIDAsync(1))
                .ReturnsAsync(new PreferredVersion { CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = "Ultra Rare" });
            var page = CreatePage();
            page.ID = 1;
            await page.OnGetAsync();

            var result = page.GetCompletionStatus(CollectionStatus.Owned, 1, "LOB-EN001", "Ultra Rare");

            Assert.AreEqual(CollectionCompletionStatus.Incomplete, result);
        }

        [TestMethod]
        public void GetCompletionStatus_StatusIsOrdered_ReturnsNull()
        {
            var page = CreatePage();

            var result = page.GetCompletionStatus(CollectionStatus.Ordered, 3, "LOB-EN001", "Ultra Rare");

            Assert.IsNull(result);
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
        public async Task OnGetAsync_MultipleEntriesInSameGroup_OwnedBeatsOrderedAndSumsQuantity()
        {
            _cardServiceMock.Setup(s => s.GetCardByID(1)).Returns(new Card { ID = 1 });
            _cardServiceMock.Setup(s => s.GetEntriesByCardIDAsync(1)).ReturnsAsync(
            [
                new CollectionEntry { CardID = 1, SetCode = "LOB-EN001", RarityName = "Ultra Rare", Status = CollectionStatus.Ordered, Quantity = 1 },
                new CollectionEntry { CardID = 1, SetCode = "LOB-EN001", RarityName = "Ultra Rare", Status = CollectionStatus.Owned, Quantity = 2 }
            ]);
            var page = CreatePage();
            page.ID = 1;

            await page.OnGetAsync();

            var summary = page.CollectionEntriesBySetCode[("LOB-EN001", "Ultra Rare")];
            Assert.AreEqual(CollectionStatus.Owned, summary.Status);
            Assert.AreEqual(3, summary.TotalQuantity);
        }

        [TestMethod]
        public async Task OnGetAsync_ValidCard_PopulatesPreferredVersionAndIsIgnored()
        {
            _cardServiceMock.Setup(s => s.GetCardByID(1)).Returns(new Card { ID = 1, Name = "Dark Magician" });
            _cardServiceMock.Setup(s => s.GetEntriesByCardIDAsync(1)).ReturnsAsync([]);
            var pv = new PreferredVersion { CardID = 1, ImageID = 10, SetCode = "LOB-EN001" };
            _cardServiceMock.Setup(s => s.GetPreferredVersionByCardIDAsync(1)).ReturnsAsync(pv);
            _cardServiceMock.Setup(s => s.IsCardIgnoredAsync(1)).ReturnsAsync(true);
            var page = CreatePage();
            page.ID = 1;

            await page.OnGetAsync();

            Assert.IsFalse(page.CardNotFound);
            Assert.AreSame(pv, page.PreferredVersion);
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
            page.ImageID = 10;
            page.SetCode = "LOB-EN001";

            var result = await page.OnPostOrderAsync(quantity: 2, edition: null);

            _cardServiceMock.Verify(s => s.AddEntryAsync(
                1, 10, "LOB-EN001", CollectionStatus.Ordered, 2,
                null, null, null, null, null, null, null), Times.Once);
            Assert.IsInstanceOfType<RedirectToPageResult>(result);
        }

        [TestMethod]
        public async Task OnPostOrderAsync_SetAsPreferredTrue_AlsoSavesPreferredVersion()
        {
            var page = CreatePage();
            page.CardID = 1;
            page.ImageID = 10;
            page.SetCode = "LOB-EN001";

            await page.OnPostOrderAsync(setAsPreferred: true);

            _cardServiceMock.Verify(s => s.SavePreferredVersionAsync(1, 10, "LOB-EN001", null), Times.Once);
        }

        [TestMethod]
        public async Task OnPostOwnAsync_AddsOwnedEntryAndRedirects()
        {
            var page = CreatePage();
            page.CardID = 1;
            page.ImageID = 10;
            page.SetCode = "LOB-EN001";

            var result = await page.OnPostOwnAsync(quantity: 3);

            _cardServiceMock.Verify(s => s.AddEntryAsync(
                1, 10, "LOB-EN001", CollectionStatus.Owned, 3,
                null, null, null, null, null, null, null), Times.Once);
            Assert.IsInstanceOfType<RedirectToPageResult>(result);
        }

        [TestMethod]
        public async Task OnPostOwnAsync_SetAsPreferredTrue_AlsoSavesPreferredVersion()
        {
            var page = CreatePage();
            page.CardID = 1;
            page.ImageID = 10;
            page.SetCode = "LOB-EN001";

            await page.OnPostOwnAsync(quantity: 3, setAsPreferred: true);

            _cardServiceMock.Verify(s => s.SavePreferredVersionAsync(1, 10, "LOB-EN001", null), Times.Once);
        }

        [TestMethod]
        public async Task OnPostRemovePreferredAsync_RemovesFromWishlistAndRedirects()
        {
            var page = CreatePage();
            page.ImageID = 10;

            var result = await page.OnPostRemovePreferredAsync();

            _cardServiceMock.Verify(s => s.RemoveFromWishlistAsync(10), Times.Once);
            Assert.IsInstanceOfType<RedirectToPageResult>(result);
        }

        [TestMethod]
        public async Task OnPostSetPreferredAsync_SavesPreferredVersionAndRedirects()
        {
            var page = CreatePage();
            page.CardID = 1;
            page.ImageID = 10;
            page.SetCode = "LOB-EN001";
            page.RarityName = "Ultra Rare";

            var result = await page.OnPostSetPreferredAsync();

            _cardServiceMock.Verify(s => s.SavePreferredVersionAsync(1, 10, "LOB-EN001", "Ultra Rare"), Times.Once);
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
