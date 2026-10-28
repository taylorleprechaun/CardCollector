using CardCollector.Data.Models;
using CardCollector.Pages;
using CardCollector.Repository;
using CardCollector.Services;
using CardCollector.Tests.TestHelpers;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Moq;

namespace CardCollector.Tests.Pages
{
    [TestClass]
    public sealed class OrdersModelTests
    {
        private Mock<ICardService> _cardServiceMock = null!;
        private Mock<ICollectionRepository> _collectionRepositoryMock = null!;
        private Mock<IRazorPartialRenderer> _razorPartialRendererMock = null!;

        [TestMethod]
        public void GetFilterRouteValues_SearchFiltersAndPage_IncludesEachAndLeavesBlankOnesOut()
        {
            var page = CreatePage();
            page.PageNumber = 2;
            page.PageSize = 25;
            page.Query = "magician";
            page.RarityName = "Ultra Rare";
            page.SortDescending = true;

            var values = page.GetFilterRouteValues();

            Assert.AreEqual("2", values["pageNumber"]);
            Assert.AreEqual("25", values["pageSize"]);
            Assert.AreEqual("magician", values["query"]);
            Assert.AreEqual("Ultra Rare", values["rarityName"]);
            Assert.AreEqual("True", values["sortDescending"]);
            Assert.IsFalse(values.ContainsKey("cardType"));
            Assert.IsFalse(values.ContainsKey("setName"));
        }

        [TestMethod]
        public void GetPaginationParams_SortDescending_IncludesSortAndFilters()
        {
            var page = CreatePage();
            page.SetName = "Metal Raiders";
            page.SortDescending = true;

            var values = page.GetPaginationParams();

            Assert.AreEqual("Metal Raiders", values["setName"]);
            Assert.AreEqual("True", values["sortDescending"]);
        }

        [TestMethod]
        public async Task OnGetAsync_SearchResult_ExposesPageCountsTotalAndFilterOptions()
        {
            SetUpSearchResult(new OrderSearchResult
            {
                AvailableRarityNames = ["Ultra Rare"],
                AvailableSetNames = ["Metal Raiders"],
                PagedItems = new PagedResult<EditionAuditEntryViewModel>
                {
                    Items = [new EditionAuditEntryViewModel { EntryID = 1, Quantity = 3 }],
                    Page = 1,
                    PageSize = 50,
                    TotalCount = 4,
                    TotalQuantitySum = 9
                },
                TotalCost = 12.50m
            });
            var page = CreatePage();

            await page.OnGetAsync();

            Assert.AreEqual(1, page.Results.Items.Count);
            Assert.AreEqual(4, page.Results.TotalCount);
            Assert.AreEqual(9, page.CardCount);
            Assert.AreEqual(12.50m, page.TotalCost);
            CollectionAssert.AreEqual(new[] { "Ultra Rare" }, page.AvailableRarityNames.ToArray());
            CollectionAssert.AreEqual(new[] { "Metal Raiders" }, page.AvailableSetNames.ToArray());
        }

        [TestMethod]
        public async Task OnGetAsync_SearchAndFilters_PassesTrimmedValuesToCardService()
        {
            OrderSearchCriteria? captured = null;
            _cardServiceMock.Setup(s => s.SearchOrdersAsync(It.IsAny<OrderSearchCriteria>()))
                .Callback<OrderSearchCriteria>(c => captured = c)
                .ReturnsAsync(new OrderSearchResult());
            var page = CreatePage();
            page.CardType = "Monster";
            page.PageNumber = 2;
            page.Query = "  magician  ";
            page.RarityName = "Ultra Rare";
            page.SetName = "Metal Raiders";
            page.SortDescending = true;

            await page.OnGetAsync();

            Assert.AreEqual("Monster", captured!.CardType);
            Assert.AreEqual(2, captured.Page);
            Assert.AreEqual("magician", captured.Query);
            Assert.AreEqual("Ultra Rare", captured.RarityName);
            Assert.AreEqual("Metal Raiders", captured.SetName);
            Assert.IsTrue(captured.SortDescending);
        }

        [TestMethod]
        public async Task OnGetOrderAutocompleteAsync_BlankQuery_ReturnsEmptyWithoutAskingTheService()
        {
            var page = CreatePage();

            var result = (JsonResult)await page.OnGetOrderAutocompleteAsync("  ");

            Assert.AreEqual(0, ((string[])result.Value!).Length);
            _cardServiceMock.Verify(s => s.GetOrderedCardNameSuggestionsAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        }

        [TestMethod]
        public async Task OnGetOrderAutocompleteAsync_Query_ReturnsOrderedCardNameSuggestions()
        {
            _cardServiceMock.Setup(s => s.GetOrderedCardNameSuggestionsAsync("dark", It.IsAny<int>()))
                .ReturnsAsync((IReadOnlyList<string>)["Dark Magician"]);
            var page = CreatePage();

            var result = (JsonResult)await page.OnGetOrderAutocompleteAsync(" dark ");

            CollectionAssert.AreEqual(new[] { "Dark Magician" }, ((IReadOnlyList<string>)result.Value!).ToArray());
        }

        [TestMethod]
        public async Task OnPostDeleteAsync_ActiveSearch_RedirectKeepsTheSearchAndPage()
        {
            var page = CreatePage();
            page.PageNumber = 2;
            page.Query = "magician";

            var result = (RedirectToPageResult)await page.OnPostDeleteAsync(5);

            Assert.AreEqual("magician", result.RouteValues!["query"]);
            Assert.AreEqual("2", result.RouteValues["pageNumber"]);
        }

        [TestMethod]
        public async Task OnPostDeleteAsync_DeletesEntryAndRedirects()
        {
            var page = CreatePage();

            var result = await page.OnPostDeleteAsync(5);

            _collectionRepositoryMock.Verify(r => r.DeleteAsync(5), Times.Once);
            Assert.IsInstanceOfType<RedirectToPageResult>(result);
        }

        [TestMethod]
        public async Task OnPostDeleteAsync_NoSearch_RedirectLeavesTheQueryOut()
        {
            var page = CreatePage();

            var result = (RedirectToPageResult)await page.OnPostDeleteAsync(5);

            Assert.IsFalse(result.RouteValues!.ContainsKey("query"));
        }

        [TestMethod]
        public async Task OnPostEditAsync_AjaxRequest_SendsMatchCountCardCountAndTotalHeaders()
        {
            SetUpSearchResult(new OrderSearchResult
            {
                PagedItems = new PagedResult<EditionAuditEntryViewModel>
                {
                    Items = [new EditionAuditEntryViewModel { EntryID = 1, Quantity = 2 }],
                    TotalCount = 7,
                    TotalQuantitySum = 12
                },
                TotalCost = 14.50m
            });
            var page = CreatePage(isAjax: true);

            await page.OnPostEditAsync(1, 2, null, null, null, null, null, null);

            var headers = page.HttpContext.Response.Headers;
            Assert.AreEqual("7", headers["X-Orders-Count"].ToString());
            Assert.AreEqual("12", headers["X-Orders-Cards"].ToString());
            Assert.AreEqual("14.50", headers["X-Orders-Total"].ToString());
        }

        [TestMethod]
        public async Task OnPostEditAsync_AjaxRequestNoMatch_ReturnsEmptyContent()
        {
            SetUpSearchResult(new OrderSearchResult());
            var page = CreatePage(isAjax: true);

            var result = await page.OnPostEditAsync(999, 2, null, null, null, null, null, null) as ContentResult;

            Assert.AreEqual(string.Empty, result!.Content);
        }

        [TestMethod]
        public async Task OnPostEditAsync_AjaxRequestWithMatch_ReturnsRenderedPartial()
        {
            SetUpSearchResult(new OrderSearchResult
            {
                PagedItems = new PagedResult<EditionAuditEntryViewModel> { Items = [new EditionAuditEntryViewModel { EntryID = 1, CardName = "Dark Magician" }] }
            });
            _razorPartialRendererMock
                .Setup(r => r.RenderPartialAsync(It.IsAny<PageModel>(), "_OrderRow", It.IsAny<EditionAuditEntryViewModel>()))
                .ReturnsAsync("<tr>rendered</tr>");
            var page = CreatePage(isAjax: true);

            var result = await page.OnPostEditAsync(1, 2, null, null, null, null, null, null) as ContentResult;

            Assert.AreEqual("<tr>rendered</tr>", result!.Content);
        }

        [TestMethod]
        public async Task OnPostEditAsync_NonAjaxRequest_RedirectsToPage()
        {
            var page = CreatePage(isAjax: false);

            var result = await page.OnPostEditAsync(1, 2, null, null, null, null, null, null);

            _collectionRepositoryMock.Verify(r => r.UpdateAsync(It.Is<CollectionEntry>(e => e.ID == 1 && e.Quantity == 2)), Times.Once);
            Assert.IsInstanceOfType<RedirectToPageResult>(result);
        }

        [TestMethod]
        public async Task OnPostEditAsync_QuantityBelowOne_ClampsToOne()
        {
            CollectionEntry? captured = null;
            _collectionRepositoryMock
                .Setup(r => r.UpdateAsync(It.IsAny<CollectionEntry>()))
                .Callback<CollectionEntry>(e => captured = e)
                .ReturnsAsync(true);
            var page = CreatePage();

            await page.OnPostEditAsync(1, 0, null, null, null, null, null, null);

            Assert.AreEqual(1, captured!.Quantity);
        }

        [TestMethod]
        public async Task OnPostMarkOwnedAsync_QuantityBelowOne_ClampsToOne()
        {
            var page = CreatePage();

            await page.OnPostMarkOwnedAsync(1, 0);

            _collectionRepositoryMock.Verify(r => r.UpdateStatusAsync(1, CollectionStatus.Owned, 1), Times.Once);
        }

        [TestMethod]
        public async Task OnPostMarkOwnedAsync_RedirectsToPage()
        {
            var page = CreatePage();

            var result = await page.OnPostMarkOwnedAsync(1, 2);

            Assert.IsInstanceOfType<RedirectToPageResult>(result);
        }

        [TestInitialize]
        public void Setup()
        {
            _cardServiceMock = new Mock<ICardService>();
            _collectionRepositoryMock = new Mock<ICollectionRepository>();
            _razorPartialRendererMock = new Mock<IRazorPartialRenderer>();
        }

        private OrdersModel CreatePage(bool isAjax = false)
        {
            var page = new OrdersModel(_cardServiceMock.Object, _collectionRepositoryMock.Object, _razorPartialRendererMock.Object);
            PageContextFactory.Attach(page, httpContext =>
            {
                if (isAjax)
                    httpContext.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
            });
            return page;
        }

        private void SetUpSearchResult(OrderSearchResult result) =>
            _cardServiceMock.Setup(s => s.SearchOrdersAsync(It.IsAny<OrderSearchCriteria>())).ReturnsAsync(result);
    }
}
