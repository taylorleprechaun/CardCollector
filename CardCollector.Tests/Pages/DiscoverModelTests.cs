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
    public sealed class DiscoverModelTests
    {
        private Mock<ICardService> _cardServiceMock = null!;
        private Mock<ICardSetRepository> _cardSetRepositoryMock = null!;

        [TestMethod]
        public void GetTCGDate_DelegatesToCardSetRepository()
        {
            _cardSetRepositoryMock.Setup(r => r.GetTCGDateBySetCode("LOB-EN001")).Returns("2002-03-08");
            var page = CreatePage();

            var result = page.GetTCGDate("LOB-EN001");

            Assert.AreEqual("2002-03-08", result);
        }

        [TestMethod]
        public async Task OnGetAsync_NoUncollectedCard_SetsIsComplete()
        {
            _cardServiceMock.Setup(s => s.GetRandomUncollectedAsync()).ReturnsAsync((Card?)null);
            var page = CreatePage();

            await page.OnGetAsync();

            Assert.IsTrue(page.IsComplete);
            Assert.IsNull(page.CurrentCard);
        }

        [TestMethod]
        public async Task OnGetAsync_UncollectedCardExists_SetsCurrentCardAndImage()
        {
            var card = new Card { ID = 1, Name = "Dark Magician", CardImages = [new Image { ID = 10 }] };
            _cardServiceMock.Setup(s => s.GetRandomUncollectedAsync()).ReturnsAsync(card);
            var page = CreatePage();

            await page.OnGetAsync();

            Assert.IsFalse(page.IsComplete);
            Assert.AreSame(card, page.CurrentCard);
            Assert.AreEqual(10, page.CurrentImage!.ID);
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
        public async Task OnPostSetPreferredAsync_SavesPreferredVersionAndRedirects()
        {
            var page = CreatePage();
            page.CardID = 5;
            page.ImageID = 10;
            page.SetCode = "LOB-EN001";
            page.RarityName = "Ultra Rare";

            var result = await page.OnPostSetPreferredAsync();

            _cardServiceMock.Verify(s => s.SavePreferredVersionAsync(5, 10, "LOB-EN001", "Ultra Rare"), Times.Once);
            Assert.IsInstanceOfType<RedirectToPageResult>(result);
        }

        [TestInitialize]
        public void Setup()
        {
            _cardServiceMock = new Mock<ICardService>();
            _cardSetRepositoryMock = new Mock<ICardSetRepository>();
        }

        private DiscoverModel CreatePage()
        {
            var page = new DiscoverModel(_cardServiceMock.Object, _cardSetRepositoryMock.Object);
            PageContextFactory.Attach(page);
            return page;
        }
    }
}
