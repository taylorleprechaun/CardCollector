using CardCollector.Pages.Catalog;
using CardCollector.Services;
using CardCollector.Tests.TestHelpers;
using CardCollector.ViewModels;
using Moq;

namespace CardCollector.Tests.Pages.Catalog
{
    [TestClass]
    public sealed class SyncsModelTests
    {
        [TestMethod]
        public async Task OnGetAsync_LoadsRequestedPageOfHistory()
        {
            var history = new CatalogSyncHistoryViewModel { WarningRuns = 3 };
            var serviceMock = new Mock<ICatalogAdminService>();
            serviceMock.Setup(s => s.GetSyncHistoryAsync(2, It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(history);
            var page = new SyncsModel(serviceMock.Object) { PageNumber = 2 };
            PageContextFactory.Attach(page);

            await page.OnGetAsync();

            Assert.AreSame(history, page.History);
        }
    }
}
