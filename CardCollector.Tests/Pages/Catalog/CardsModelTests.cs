using CardCollector.Data.Models.Catalog;
using CardCollector.Pages.Catalog;
using CardCollector.Services;
using CardCollector.Tests.TestHelpers;
using CardCollector.ViewModels;
using Moq;

namespace CardCollector.Tests.Pages.Catalog
{
    [TestClass]
    public sealed class CardsModelTests
    {
        [TestMethod]
        public void GetPaginationParams_QueryAndStatus_KeepsBoth()
        {
            var page = new CardsModel(new Mock<ICatalogAdminService>().Object) { Query = "theo", Status = CatalogStatus.Retired };

            var result = page.GetPaginationParams();

            Assert.AreEqual("theo", result["q"]);
            Assert.AreEqual("Retired", result["status"]);
        }

        [TestMethod]
        public async Task OnGetAsync_PassesQueryAndStatusToSearch()
        {
            var serviceMock = new Mock<ICatalogAdminService>();
            serviceMock.Setup(s => s.SearchCardsAsync(
                    It.Is<CatalogCardSearchCriteria>(c => c.Query == "theo" && c.Status == CatalogStatus.Retired && c.Page == 1),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PagedResult<CatalogCardRowViewModel> { TotalCount = 1 });
            var page = new CardsModel(serviceMock.Object) { PageNumber = 0, Query = "theo", Status = CatalogStatus.Retired };
            PageContextFactory.Attach(page);

            await page.OnGetAsync();

            Assert.AreEqual(1, page.Results.TotalCount);
            Assert.AreEqual(1, page.PageNumber);
        }
    }
}
