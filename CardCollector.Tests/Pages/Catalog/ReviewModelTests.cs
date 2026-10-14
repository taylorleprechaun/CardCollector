using CardCollector.Data.Models.Catalog;
using CardCollector.Models;
using CardCollector.Pages.Catalog;
using CardCollector.Services;
using CardCollector.Tests.TestHelpers;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Moq;

namespace CardCollector.Tests.Pages.Catalog
{
    [TestClass]
    public sealed class ReviewModelTests
    {
        private Mock<ICatalogReviewService> _catalogReviewServiceMock = null!;
        private Mock<IRazorPartialRenderer> _razorPartialRendererMock = null!;

        [TestMethod]
        public async Task OnGetAsync_LoadsResultsAndOpenCounts()
        {
            _catalogReviewServiceMock.Setup(s => s.GetOpenCountsByKindAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<CatalogReviewItemKind, int> { [CatalogReviewItemKind.PasscodeChanged] = 2, [CatalogReviewItemKind.CardAdded] = 5 });
            _catalogReviewServiceMock.Setup(s => s.SearchAsync(It.IsAny<CatalogReviewSearchCriteria>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PagedResult<CatalogReviewItemViewModel> { TotalCount = 2 });
            var page = CreatePage();

            await page.OnGetAsync();

            Assert.AreEqual(2, page.Results.TotalCount);
            Assert.AreEqual(2, page.CountOpen(actionable: true));
            Assert.AreEqual(5, page.CountOpen(actionable: false));
        }

        [TestMethod]
        public async Task OnPostAcceptAsync_AjaxFailure_RendersRowWithErrorMessage()
        {
            _catalogReviewServiceMock.Setup(s => s.AcceptAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(SaveResult.Failure(["Still in use."]));
            _catalogReviewServiceMock.Setup(s => s.SearchAsync(It.IsAny<CatalogReviewSearchCriteria>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PagedResult<CatalogReviewItemViewModel> { TotalCount = 1 });
            _catalogReviewServiceMock.Setup(s => s.GetItemAsync(7, "Still in use.", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CatalogReviewItemViewModel { ErrorMessage = "Still in use.", ID = 7, Status = CatalogReviewItemStatus.Open });
            _razorPartialRendererMock
                .Setup(r => r.RenderPartialAsync(It.IsAny<PageModel>(), "_ReviewItemRow", It.Is<CatalogReviewRowViewModel>(m => m.Item.ErrorMessage == "Still in use.")))
                .ReturnsAsync("<div>row</div>");
            var page = CreatePage(isAjax: true);

            var result = await page.OnPostAcceptAsync(7) as ContentResult;

            Assert.AreEqual("<div>row</div>", result!.Content);
        }

        [TestMethod]
        public async Task OnPostAcceptAsync_AjaxSuccess_RemovesRowAndSetsCountHeaders()
        {
            _catalogReviewServiceMock.Setup(s => s.AcceptAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(SaveResult.Success());
            _catalogReviewServiceMock.Setup(s => s.GetItemAsync(7, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CatalogReviewItemViewModel { ID = 7, Status = CatalogReviewItemStatus.Accepted });
            _catalogReviewServiceMock.Setup(s => s.CountNeedingDecisionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(3);
            _catalogReviewServiceMock.Setup(s => s.SearchAsync(It.IsAny<CatalogReviewSearchCriteria>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PagedResult<CatalogReviewItemViewModel> { TotalCount = 3 });
            var page = CreatePage(isAjax: true);

            var result = await page.OnPostAcceptAsync(7) as ContentResult;

            Assert.AreEqual(string.Empty, result!.Content);
            Assert.AreEqual("3", (string?)page.HttpContext.Response.Headers["X-Review-Count"]);
            Assert.AreEqual("3", (string?)page.HttpContext.Response.Headers["X-Total-Count"]);
        }

        [TestMethod]
        public async Task OnPostDismissAllAsync_RedirectsWithMessage()
        {
            _catalogReviewServiceMock.Setup(s => s.DismissAllInformationalAsync(CatalogReviewItemKind.CardAdded, It.IsAny<CancellationToken>())).ReturnsAsync(4);
            var page = CreatePage();
            page.Filter = CatalogReviewFilter.Informational;
            page.Kind = CatalogReviewItemKind.CardAdded;

            var result = await page.OnPostDismissAllAsync();

            Assert.IsInstanceOfType<RedirectToPageResult>(result);
            Assert.AreEqual("Dismissed 4 informational items.", page.TempData["Success"]);
        }

        [TestMethod]
        public async Task OnPostRejectAsync_NotAjaxFailure_RedirectsWithWarning()
        {
            _catalogReviewServiceMock.Setup(s => s.RejectAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(SaveResult.Missing("That review item is not open."));
            var page = CreatePage();

            var result = await page.OnPostRejectAsync(7);

            Assert.IsInstanceOfType<RedirectToPageResult>(result);
            Assert.AreEqual("That review item is not open.", page.TempData["Warning"]);
        }

        [TestInitialize]
        public void Setup()
        {
            _catalogReviewServiceMock = new Mock<ICatalogReviewService>();
            _razorPartialRendererMock = new Mock<IRazorPartialRenderer>();
        }

        private ReviewModel CreatePage(bool isAjax = false)
        {
            var page = new ReviewModel(_catalogReviewServiceMock.Object, _razorPartialRendererMock.Object);
            PageContextFactory.Attach(page, httpContext =>
            {
                if (isAjax)
                    httpContext.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
            });
            return page;
        }
    }
}
