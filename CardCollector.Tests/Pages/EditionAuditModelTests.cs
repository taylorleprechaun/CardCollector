using CardCollector.Data.Models;
using CardCollector.Pages;
using CardCollector.Repository;
using CardCollector.Services;
using CardCollector.Tests.TestHelpers;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Primitives;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CardCollector.Tests.Pages
{
    [TestClass]
    public sealed class EditionAuditModelTests
    {
        private Mock<ICardDataRepository> _cardDataRepositoryMock = null!;
        private Mock<ICardService> _cardServiceMock = null!;
        private Mock<ICollectionRepository> _collectionRepositoryMock = null!;
        private Mock<IRazorPartialRenderer> _razorPartialRendererMock = null!;

        [TestMethod]
        public void ActiveFilterCount_CategorySet_AddsOneToBaseCount()
        {
            var page = CreatePage();
            page.Category = EditionAuditCategory.EditionMismatch;

            Assert.AreEqual(1, page.ActiveFilterCount);
        }

        [TestMethod]
        public void ActiveFilterCount_NoCategorySet_ReturnsBaseCount()
        {
            var page = CreatePage();

            Assert.AreEqual(0, page.ActiveFilterCount);
        }

        [TestMethod]
        public void GetFilterParams_IncludesRarityNameFromQueryString()
        {
            var page = CreatePage(rarityNameQuery: "Ultra Rare");

            var result = page.GetFilterParams();

            StringAssert.Contains(result, "rarityName=Ultra%20Rare");
        }

        [TestMethod]
        public void HasActiveFilters_CategorySet_ReturnsTrue()
        {
            var page = CreatePage();
            page.Category = EditionAuditCategory.Unverifiable;

            Assert.IsTrue(page.HasActiveFilters);
        }

        [TestMethod]
        public void HasActiveFilters_NoFiltersSet_ReturnsFalse()
        {
            var page = CreatePage();

            Assert.IsFalse(page.HasActiveFilters);
        }

        [TestMethod]
        public async Task OnGetAsync_PopulatesAvailableFiltersAndResults()
        {
            _collectionRepositoryMock.Setup(r => r.GetDistinctSetCodesAsync()).ReturnsAsync((IReadOnlyList<string>)["LOB-EN001"]);
            _collectionRepositoryMock.Setup(r => r.GetDistinctRarityNamesAsync()).ReturnsAsync((IReadOnlyList<string>)["Ultra Rare"]);
            _cardServiceMock.Setup(s => s.SearchEditionAuditAsync(It.IsAny<EditionAuditSearchCriteria>()))
                .ReturnsAsync(new PagedResult<EditionAuditGroupViewModel> { TotalCount = 2 });
            var page = CreatePage();

            await page.OnGetAsync();

            Assert.AreEqual(2, page.Results.TotalCount);
            CollectionAssert.AreEqual(new[] { "Ultra Rare" }, page.AvailableRarityNames.ToArray());
        }

        [TestMethod]
        public async Task OnPostEditAsync_AjaxNoExistingEntry_ReturnsEmptyContent()
        {
            _collectionRepositoryMock.Setup(r => r.GetByIDAsync(999)).ReturnsAsync((CollectionEntry?)null);
            _cardServiceMock.Setup(s => s.SearchEditionAuditAsync(It.IsAny<EditionAuditSearchCriteria>()))
                .ReturnsAsync(new PagedResult<EditionAuditGroupViewModel>());
            var page = CreatePage(isAjax: true);

            var result = await page.OnPostEditAsync(999, 2, null, null, null, null, null, null) as ContentResult;

            Assert.AreEqual(string.Empty, result!.Content);
        }

        [TestMethod]
        public async Task OnPostEditAsync_AjaxWithMatch_ReturnsRenderedPartial()
        {
            _collectionRepositoryMock.Setup(r => r.GetByIDAsync(1))
                .ReturnsAsync(new CollectionEntry { ID = 1, CardID = 5, SetCode = "LOB-EN001" });
            _cardServiceMock.Setup(s => s.SearchEditionAuditAsync(It.IsAny<EditionAuditSearchCriteria>()))
                .ReturnsAsync(new PagedResult<EditionAuditGroupViewModel>
                {
                    Items = [new EditionAuditGroupViewModel { CardID = 5, SetCode = "LOB-EN001" }]
                });
            _razorPartialRendererMock
                .Setup(r => r.RenderPartialAsync(It.IsAny<PageModel>(), "_EditionAuditGroupRow", It.IsAny<EditionAuditGroupRowViewModel>()))
                .ReturnsAsync("<tr>rendered</tr>");
            var page = CreatePage(isAjax: true);

            var result = await page.OnPostEditAsync(1, 2, null, null, null, null, null, null) as ContentResult;

            Assert.AreEqual("<tr>rendered</tr>", result!.Content);
        }

        [TestMethod]
        public async Task OnPostEditAsync_NonAjax_UpdatesEntryAndRedirects()
        {
            var page = CreatePage(isAjax: false);

            var result = await page.OnPostEditAsync(1, 2, null, null, null, null, null, null);

            _collectionRepositoryMock.Verify(r => r.UpdateAsync(It.Is<CollectionEntry>(e => e.ID == 1)), Times.Once);
            Assert.IsInstanceOfType<RedirectToPageResult>(result);
        }

        [TestInitialize]
        public void Setup()
        {
            _cardDataRepositoryMock = new Mock<ICardDataRepository>();
            _cardDataRepositoryMock.Setup(r => r.GetSetNamesByCode()).Returns(new Dictionary<string, string>());
            _cardServiceMock = new Mock<ICardService>();
            _collectionRepositoryMock = new Mock<ICollectionRepository>();
            _collectionRepositoryMock.Setup(r => r.GetDistinctSetCodesAsync()).ReturnsAsync((IReadOnlyList<string>)[]);
            _razorPartialRendererMock = new Mock<IRazorPartialRenderer>();
        }

        private EditionAuditModel CreatePage(bool isAjax = false, string? rarityNameQuery = null)
        {
            var page = new EditionAuditModel(
                _cardDataRepositoryMock.Object, _cardServiceMock.Object, _collectionRepositoryMock.Object, _razorPartialRendererMock.Object);
            PageContextFactory.Attach(page, httpContext =>
            {
                if (isAjax)
                    httpContext.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
                if (rarityNameQuery is not null)
                    httpContext.Request.Query = new QueryCollection(new Dictionary<string, StringValues> { ["rarityName"] = rarityNameQuery });
            });
            return page;
        }
    }
}
