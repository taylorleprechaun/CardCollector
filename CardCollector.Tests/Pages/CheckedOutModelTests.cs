using CardCollector.Pages;
using CardCollector.Services;
using CardCollector.Tests.TestHelpers;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CardCollector.Tests.Pages
{
    [TestClass]
    public sealed class CheckedOutModelTests
    {
        private Mock<ICardService> _cardServiceMock = null!;

        [TestMethod]
        public async Task OnGetAsync_PopulatesResultsFromCardService()
        {
            _cardServiceMock.Setup(s => s.SearchCheckedOutAsync(It.IsAny<CheckedOutSearchCriteria>()))
                .ReturnsAsync(new PagedResult<CheckedOutCardViewModel> { TotalCount = 3 });
            var page = CreatePage();

            await page.OnGetAsync();

            Assert.AreEqual(3, page.Results.TotalCount);
        }

        [TestMethod]
        public async Task OnPostCheckInAsync_ChecksInAndRedirects()
        {
            var page = CreatePage();

            var result = await page.OnPostCheckInAsync(10, "LOB-EN001", "Ultra Rare");

            _cardServiceMock.Verify(s => s.CheckInCardAsync(10, "LOB-EN001", "Ultra Rare"), Times.Once);
            Assert.IsInstanceOfType<RedirectToPageResult>(result);
        }

        [TestMethod]
        public async Task OnPostCheckOutAsync_QuantityAtLeastOne_ChecksOutCard()
        {
            var page = CreatePage();

            await page.OnPostCheckOutAsync(1, 10, "LOB-EN001", "Ultra Rare", 2);

            _cardServiceMock.Verify(s => s.CheckOutCardAsync(1, 10, "LOB-EN001", "Ultra Rare", 2), Times.Once);
        }

        [TestMethod]
        public async Task OnPostCheckOutAsync_QuantityIsZero_DoesNotCheckOutCard()
        {
            var page = CreatePage();

            await page.OnPostCheckOutAsync(1, 10, "LOB-EN001", "Ultra Rare", 0);

            _cardServiceMock.Verify(s => s.CheckOutCardAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        }

        [TestInitialize]
        public void Setup() => _cardServiceMock = new Mock<ICardService>();

        private CheckedOutModel CreatePage()
        {
            var page = new CheckedOutModel(_cardServiceMock.Object);
            PageContextFactory.Attach(page);
            return page;
        }
    }
}
