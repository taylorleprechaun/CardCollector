using CardCollector.Pages;
using CardCollector.Services;
using CardCollector.Tests.TestHelpers;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Moq;

namespace CardCollector.Tests.Pages
{
    [TestClass]
    public sealed class CheckedOutModelTests
    {
        private Mock<ICardService> _cardServiceMock = null!;
        private Mock<IRazorPartialRenderer> _razorPartialRendererMock = null!;

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
        public async Task OnPostCheckInAsync_AjaxWithMatch_ReturnsRenderedPartialAndSetsHeader()
        {
            _cardServiceMock.Setup(s => s.SearchCheckedOutAsync(It.IsAny<CheckedOutSearchCriteria>()))
                .ReturnsAsync(new PagedResult<CheckedOutCardViewModel>
                {
                    TotalCount = 1,
                    Items = [new CheckedOutCardViewModel { ImageID = 10, SetCode = "LOB-EN001", RarityName = "Ultra Rare" }]
                });
            _razorPartialRendererMock
                .Setup(r => r.RenderPartialAsync(It.IsAny<PageModel>(), "_CheckedOutRow", It.IsAny<CheckedOutRowViewModel>()))
                .ReturnsAsync("<div>row</div>");
            var page = CreatePage(isAjax: true);

            var result = await page.OnPostCheckInAsync(10, "LOB-EN001", "Ultra Rare") as ContentResult;

            Assert.AreEqual("<div>row</div>", result!.Content);
            Assert.AreEqual("1", (string?)page.HttpContext.Response.Headers["X-Total-Count"]);
        }

        [TestMethod]
        public async Task OnPostCheckInAsync_AjaxWithNoMatchingRow_ReturnsEmptyContent()
        {
            _cardServiceMock.Setup(s => s.SearchCheckedOutAsync(It.IsAny<CheckedOutSearchCriteria>()))
                .ReturnsAsync(new PagedResult<CheckedOutCardViewModel>());
            var page = CreatePage(isAjax: true);

            var result = await page.OnPostCheckInAsync(10, "LOB-EN001", "Ultra Rare") as ContentResult;

            Assert.AreEqual(string.Empty, result!.Content);
        }

        [TestMethod]
        public async Task OnPostCheckInAsync_ChecksInAndRedirectsWhenNotAjax()
        {
            _cardServiceMock.Setup(s => s.SearchCheckedOutAsync(It.IsAny<CheckedOutSearchCriteria>()))
                .ReturnsAsync(new PagedResult<CheckedOutCardViewModel>());
            var page = CreatePage(isAjax: false);

            var result = await page.OnPostCheckInAsync(10, "LOB-EN001", "Ultra Rare");

            _cardServiceMock.Verify(s => s.CheckInCardAsync(10, "LOB-EN001", "Ultra Rare"), Times.Once);
            Assert.IsInstanceOfType<RedirectToPageResult>(result);
        }

        [TestMethod]
        public async Task OnPostCheckOutAsync_QuantityAtLeastOne_ChecksOutCard()
        {
            _cardServiceMock.Setup(s => s.SearchCheckedOutAsync(It.IsAny<CheckedOutSearchCriteria>()))
                .ReturnsAsync(new PagedResult<CheckedOutCardViewModel>());
            var page = CreatePage(isAjax: false);

            await page.OnPostCheckOutAsync(1, 10, "LOB-EN001", "Ultra Rare", 2);

            _cardServiceMock.Verify(s => s.CheckOutCardAsync(1, 10, "LOB-EN001", "Ultra Rare", 2), Times.Once);
        }

        [TestMethod]
        public async Task OnPostCheckOutAsync_QuantityIsZero_DoesNotCheckOutCard()
        {
            _cardServiceMock.Setup(s => s.SearchCheckedOutAsync(It.IsAny<CheckedOutSearchCriteria>()))
                .ReturnsAsync(new PagedResult<CheckedOutCardViewModel>());
            var page = CreatePage(isAjax: false);

            await page.OnPostCheckOutAsync(1, 10, "LOB-EN001", "Ultra Rare", 0);

            _cardServiceMock.Verify(s => s.CheckOutCardAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        }

        [TestMethod]
        public async Task OnPostCheckOutAsync_RarityFilterActiveAndRowRarityDiffers_FilterSurvivesRedirect()
        {
            _cardServiceMock.Setup(s => s.SearchCheckedOutAsync(It.IsAny<CheckedOutSearchCriteria>()))
                .ReturnsAsync(new PagedResult<CheckedOutCardViewModel>());
            var page = CreatePage(isAjax: false, queryString: "?rarityName=Ultra%20Rare");

            var result = await page.OnPostCheckOutAsync(1, 10, "LOB-EN001", "Secret Rare", 2) as RedirectToPageResult;

            Assert.AreEqual("Ultra Rare", result!.RouteValues!["rarityName"]);
        }

        [TestInitialize]
        public void Setup()
        {
            _cardServiceMock = new Mock<ICardService>();
            _razorPartialRendererMock = new Mock<IRazorPartialRenderer>();
        }

        private CheckedOutModel CreatePage(bool isAjax = false, string? queryString = null)
        {
            var page = new CheckedOutModel(_cardServiceMock.Object, _razorPartialRendererMock.Object);
            PageContextFactory.Attach(page, httpContext =>
            {
                if (isAjax)
                    httpContext.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
                if (queryString is not null)
                    httpContext.Request.QueryString = new Microsoft.AspNetCore.Http.QueryString(queryString);
            });
            return page;
        }
    }
}
