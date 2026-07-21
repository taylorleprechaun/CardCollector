using CardCollector.Pages;
using CardCollector.Repository;
using CardCollector.Services;
using CardCollector.Tests.TestHelpers;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CardCollector.Tests.Pages
{
    [TestClass]
    public sealed class WishlistModelTests
    {
        private Mock<ICardService> _cardServiceMock = null!;
        private Mock<ICardSetRepository> _cardSetRepositoryMock = null!;
        private Mock<IRazorPartialRenderer> _razorPartialRendererMock = null!;

        [TestMethod]
        public async Task OnGetAsync_PopulatesAvailableFiltersAndResults()
        {
            _cardServiceMock.Setup(s => s.GetWishlistDistinctSetNamesAsync()).ReturnsAsync((IReadOnlyList<string>)["Legend"]);
            _cardServiceMock.Setup(s => s.GetWishlistDistinctRarityNamesAsync()).ReturnsAsync((IReadOnlyList<string>)["Ultra Rare"]);
            _cardServiceMock.Setup(s => s.SearchWishlistAsync(It.IsAny<WishlistSearchCriteria>()))
                .ReturnsAsync(new WishlistSearchResult { PagedItems = new PagedResult<WishlistItemViewModel> { TotalCount = 4 } });
            var page = CreatePage();

            await page.OnGetAsync();

            Assert.AreEqual(4, page.Results.TotalCount);
            CollectionAssert.AreEqual(new[] { "Legend" }, page.AvailableSetNames.ToArray());
        }

        [TestMethod]
        public async Task OnPostAddToCartAsync_BlankSetCode_ReturnsBadRequest()
        {
            var page = CreatePage();

            var result = await page.OnPostAddToCartAsync(1, 10, "  ", "Ultra Rare", 1, null);

            Assert.IsInstanceOfType<BadRequestResult>(result);
        }

        [TestMethod]
        public async Task OnPostAddToCartAsync_InvalidCardID_ReturnsBadRequest()
        {
            var page = CreatePage();

            var result = await page.OnPostAddToCartAsync(0, 10, "LOB-EN001", "Ultra Rare", 1, null);

            Assert.IsInstanceOfType<BadRequestResult>(result);
        }

        [TestMethod]
        public async Task OnPostAddToCartAsync_ValidParams_CallsAddToCartAndRedirectsWhenNotAjax()
        {
            _cardServiceMock.Setup(s => s.AddToCartAsync(1, 10, "LOB-EN001", "Ultra Rare", 1, 5m)).ReturnsAsync((1, 5m, 1));
            _cardServiceMock.Setup(s => s.SearchWishlistAsync(It.IsAny<WishlistSearchCriteria>()))
                .ReturnsAsync(new WishlistSearchResult { PagedItems = new PagedResult<WishlistItemViewModel>() });
            var page = CreatePage(isAjax: false);

            var result = await page.OnPostAddToCartAsync(1, 10, "LOB-EN001", "Ultra Rare", 1, 5m);

            _cardServiceMock.Verify(s => s.AddToCartAsync(1, 10, "LOB-EN001", "Ultra Rare", 1, 5m), Times.Once);
            Assert.IsInstanceOfType<RedirectToPageResult>(result);
        }

        [TestMethod]
        public async Task OnPostOwnAsync_AddsEntryAndRedirectsWhenNotAjax()
        {
            _cardServiceMock.Setup(s => s.SearchWishlistAsync(It.IsAny<WishlistSearchCriteria>()))
                .ReturnsAsync(new WishlistSearchResult { PagedItems = new PagedResult<WishlistItemViewModel>() });
            var page = CreatePage(isAjax: false);
            page.CardID = 1;
            page.ImageID = 10;
            page.SetCode = "LOB-EN001";
            page.Quantity = 2;

            var result = await page.OnPostOwnAsync();

            _cardServiceMock.Verify(s => s.AddEntryAsync(
                1, 10, "LOB-EN001", CardCollector.Data.Models.CollectionStatus.Owned, 2,
                null, null, null, null, null, null, null), Times.Once);
            Assert.IsInstanceOfType<RedirectToPageResult>(result);
        }

        [TestMethod]
        public async Task OnPostRemoveAsync_AjaxNoMatch_ReturnsEmptyContent()
        {
            _cardServiceMock.Setup(s => s.SearchWishlistAsync(It.IsAny<WishlistSearchCriteria>()))
                .ReturnsAsync(new WishlistSearchResult { PagedItems = new PagedResult<WishlistItemViewModel>() });
            var page = CreatePage(isAjax: true);

            var result = await page.OnPostRemoveAsync(999) as ContentResult;

            Assert.AreEqual(string.Empty, result!.Content);
        }

        [TestMethod]
        public async Task OnPostRemoveAsync_AjaxWithMatch_ReturnsRenderedPartialAndSetsHeaders()
        {
            _cardServiceMock.Setup(s => s.SearchWishlistAsync(It.IsAny<WishlistSearchCriteria>()))
                .ReturnsAsync(new WishlistSearchResult
                {
                    PagedItems = new PagedResult<WishlistItemViewModel>
                    {
                        TotalCount = 1,
                        Items = [new WishlistItemViewModel { ImageID = 10, CardName = "Dark Magician" }]
                    }
                });
            _razorPartialRendererMock
                .Setup(r => r.RenderPartialAsync(It.IsAny<PageModel>(), "_WishlistRow", It.IsAny<WishlistRowViewModel>()))
                .ReturnsAsync("<tr>rendered</tr>");
            var page = CreatePage(isAjax: true);

            var result = await page.OnPostRemoveAsync(10) as ContentResult;

            Assert.AreEqual("<tr>rendered</tr>", result!.Content);
            Assert.AreEqual("1", (string?)page.HttpContext.Response.Headers["X-Total-Count"]);
        }

        [TestMethod]
        public async Task OnPostRemoveAsync_RemovesFromWishlistAndRedirectsWhenNotAjax()
        {
            _cardServiceMock.Setup(s => s.SearchWishlistAsync(It.IsAny<WishlistSearchCriteria>()))
                .ReturnsAsync(new WishlistSearchResult { PagedItems = new PagedResult<WishlistItemViewModel>() });
            var page = CreatePage(isAjax: false);

            var result = await page.OnPostRemoveAsync(10);

            _cardServiceMock.Verify(s => s.RemoveFromWishlistAsync(10), Times.Once);
            Assert.IsInstanceOfType<RedirectToPageResult>(result);
        }

        [TestInitialize]
        public void Setup()
        {
            _cardServiceMock = new Mock<ICardService>();
            _cardSetRepositoryMock = new Mock<ICardSetRepository>();
            _razorPartialRendererMock = new Mock<IRazorPartialRenderer>();
            _cardServiceMock.Setup(s => s.GetCartSummaryAsync()).ReturnsAsync((0, 0m));
        }

        private WishlistModel CreatePage(bool isAjax = false)
        {
            var page = new WishlistModel(_cardServiceMock.Object, _cardSetRepositoryMock.Object, _razorPartialRendererMock.Object);
            PageContextFactory.Attach(page, httpContext =>
            {
                if (isAjax)
                    httpContext.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
            });
            return page;
        }
    }
}
