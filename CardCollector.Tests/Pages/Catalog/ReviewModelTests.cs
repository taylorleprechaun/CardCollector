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
        [DataRow(null, null, 429, "all 429 open informational items, of every kind", DisplayName = "Every kind")]
        [DataRow(CatalogReviewItemKind.LegacyRowAutoMapped, null, 248, "all 248 open Legacy Row Auto-Mapped items; other kinds stay open", DisplayName = "One kind")]
        [DataRow(CatalogReviewItemKind.CardAdded, null, 1, "all 1 open Card Added item; other kinds stay open", DisplayName = "One item")]
        [DataRow(null, " Token ", 12, "all 12 open informational items matching “Token”; other items stay open", DisplayName = "Search")]
        public async Task DescribeDismissAll_Filter_SaysWhatIsDismissed(CatalogReviewItemKind? kind, string? query, int count, string expected)
        {
            var page = CreatePage();
            page.Kind = kind;
            page.Query = query;
            await SetResultsAsync(page, count);

            var result = page.DescribeDismissAll();

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        [DataRow(CatalogReviewFilter.NeedsDecision, null, null, "Nothing needs a decision.", DisplayName = "Unfiltered tab")]
        [DataRow(CatalogReviewFilter.Informational, CatalogReviewItemKind.LegacyRowAutoMapped, null, "No open Legacy Row Auto-Mapped items.", DisplayName = "Kind")]
        [DataRow(CatalogReviewFilter.Informational, null, "Token", "No open informational items matching “Token”.", DisplayName = "Search")]
        [DataRow(CatalogReviewFilter.Resolved, null, "LOB", "No resolved items matching “LOB”.", DisplayName = "Resolved search")]
        public void DescribeEmpty_Filter_NamesWhatEmptiedTheList(CatalogReviewFilter filter, CatalogReviewItemKind? kind, string? query, string expected)
        {
            var page = CreatePage();
            page.Filter = filter;
            page.Kind = kind;
            page.Query = query;

            var result = page.DescribeEmpty();

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void GetFilterParams_WithSearch_KeepsTheEscapedSearchText()
        {
            var page = CreatePage();
            page.Filter = CatalogReviewFilter.Informational;
            page.Query = " Blue-Eyes & Co ";

            var result = page.GetFilterParams();

            Assert.AreEqual("filter=Informational&kind=&q=Blue-Eyes%20%26%20Co&pageNumber=1&pageSize=25", result);
        }

        [TestMethod]
        public async Task OnGetAsync_LoadsResultsAndOpenCounts()
        {
            _catalogReviewServiceMock.Setup(s => s.GetOpenCountsByKindAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<CatalogReviewItemKind, int> { [CatalogReviewItemKind.PasscodeChanged] = 2, [CatalogReviewItemKind.CardAdded] = 5 });
            _catalogReviewServiceMock.Setup(s => s.SearchAsync(It.IsAny<CatalogReviewSearchCriteria>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PagedResult<CatalogReviewItemViewModel> { Items = [new CatalogReviewItemViewModel { ID = 1 }], TotalCount = 2 });
            var page = CreatePage();

            var result = await page.OnGetAsync();

            Assert.IsInstanceOfType<PageResult>(result);
            Assert.AreEqual(2, page.Results.TotalCount);
            Assert.AreEqual(2, page.CountOpen(actionable: true));
            Assert.AreEqual(5, page.CountOpen(actionable: false));
        }

        [TestMethod]
        public async Task OnGetAsync_PagePastTheLastItem_RedirectsToTheLastPage()
        {
            _catalogReviewServiceMock.Setup(s => s.SearchAsync(It.IsAny<CatalogReviewSearchCriteria>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PagedResult<CatalogReviewItemViewModel> { Page = 3, PageSize = 25, TotalCount = 30 });
            var page = CreatePage();
            page.PageNumber = 3;
            page.Query = "Token";

            var result = await page.OnGetAsync() as RedirectToPageResult;

            Assert.AreEqual(2, result!.RouteValues!["pageNumber"]);
            Assert.AreEqual("Token", result.RouteValues["q"]);
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
            Assert.IsFalse(page.HttpContext.Response.Headers.ContainsKey("X-Ajax-Append"));
        }

        [TestMethod]
        public async Task OnPostAcceptAsync_AjaxSuccessOnFullPage_AppendsTheItemThatMovedUp()
        {
            _catalogReviewServiceMock.Setup(s => s.AcceptAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(SaveResult.Success());
            _catalogReviewServiceMock.Setup(s => s.GetItemAsync(7, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CatalogReviewItemViewModel { ID = 7, Status = CatalogReviewItemStatus.Accepted });
            _catalogReviewServiceMock
                .Setup(s => s.SearchAsync(It.Is<CatalogReviewSearchCriteria>(c => c.Page == 50 && c.PageSize == 1), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PagedResult<CatalogReviewItemViewModel> { Items = [new CatalogReviewItemViewModel { ID = 9, Status = CatalogReviewItemStatus.Open }], TotalCount = 60 });
            _razorPartialRendererMock
                .Setup(r => r.RenderPartialAsync(It.IsAny<PageModel>(), "_ReviewItemRow", It.Is<CatalogReviewRowViewModel>(m => m.Item.ID == 9)))
                .ReturnsAsync("<div>next</div>");
            var page = CreatePage(isAjax: true);
            page.PageNumber = 2;

            var result = await page.OnPostAcceptAsync(7) as ContentResult;

            Assert.AreEqual("<div>next</div>", result!.Content);
            Assert.AreEqual("true", (string?)page.HttpContext.Response.Headers["X-Ajax-Append"]);
            Assert.AreEqual("60", (string?)page.HttpContext.Response.Headers["X-Total-Count"]);
        }

        [TestMethod]
        public async Task OnPostAcceptAsync_AjaxSuccessOnLastPage_RemovesRowAndSetsCountHeaders()
        {
            _catalogReviewServiceMock.Setup(s => s.AcceptAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(SaveResult.Success());
            _catalogReviewServiceMock.Setup(s => s.GetItemAsync(7, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CatalogReviewItemViewModel { ID = 7, Status = CatalogReviewItemStatus.Accepted });
            _catalogReviewServiceMock.Setup(s => s.GetOpenCountsByKindAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<CatalogReviewItemKind, int> { [CatalogReviewItemKind.PasscodeChanged] = 3, [CatalogReviewItemKind.CardAdded] = 5 });
            _catalogReviewServiceMock.Setup(s => s.SearchAsync(It.IsAny<CatalogReviewSearchCriteria>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PagedResult<CatalogReviewItemViewModel> { TotalCount = 3 });
            var page = CreatePage(isAjax: true);

            var result = await page.OnPostAcceptAsync(7) as ContentResult;

            Assert.AreEqual(string.Empty, result!.Content);
            Assert.AreEqual("3", (string?)page.HttpContext.Response.Headers["X-Review-Count"]);
            Assert.AreEqual("3", (string?)page.HttpContext.Response.Headers["X-Total-Count"]);
            Assert.AreEqual(
                "{\"informational\":5,\"kinds\":{\"PasscodeChanged\":3,\"CardAdded\":5},\"needsDecision\":3}",
                (string?)page.HttpContext.Response.Headers["X-Review-Open-Counts"]);
        }

        [TestMethod]
        public async Task OnPostDismissAllAsync_KindAndSearch_DismissesThoseAndRedirectsWithMessage()
        {
            _catalogReviewServiceMock.Setup(s => s.DismissAllInformationalAsync(CatalogReviewItemKind.CardAdded, "Token", It.IsAny<CancellationToken>())).ReturnsAsync(4);
            var page = CreatePage();
            page.Filter = CatalogReviewFilter.Informational;
            page.Kind = CatalogReviewItemKind.CardAdded;
            page.Query = " Token ";

            var result = await page.OnPostDismissAllAsync() as RedirectToPageResult;

            Assert.AreEqual("Token", result!.RouteValues!["q"]);
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

        [TestMethod]
        public async Task OnPostReopenAsync_AjaxFromResolvedTab_RemovesRow()
        {
            _catalogReviewServiceMock.Setup(s => s.ReopenAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(SaveResult.Success());
            _catalogReviewServiceMock.Setup(s => s.GetItemAsync(7, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CatalogReviewItemViewModel { ID = 7, Status = CatalogReviewItemStatus.Open });
            _catalogReviewServiceMock.Setup(s => s.SearchAsync(It.IsAny<CatalogReviewSearchCriteria>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PagedResult<CatalogReviewItemViewModel> { TotalCount = 0 });
            var page = CreatePage(isAjax: true);
            page.Filter = CatalogReviewFilter.Resolved;

            var result = await page.OnPostReopenAsync(7) as ContentResult;

            Assert.AreEqual(string.Empty, result!.Content);
        }

        [TestInitialize]
        public void Setup()
        {
            _catalogReviewServiceMock = new Mock<ICatalogReviewService>();
            _catalogReviewServiceMock.Setup(s => s.GetOpenCountsByKindAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<CatalogReviewItemKind, int>());
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

        private async Task SetResultsAsync(ReviewModel page, int totalCount)
        {
            _catalogReviewServiceMock.Setup(s => s.SearchAsync(It.IsAny<CatalogReviewSearchCriteria>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PagedResult<CatalogReviewItemViewModel> { Items = [new CatalogReviewItemViewModel { ID = 1 }], TotalCount = totalCount });
            await page.OnGetAsync();
        }
    }
}
