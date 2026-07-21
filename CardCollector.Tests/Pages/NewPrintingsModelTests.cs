using CardCollector.Pages;
using CardCollector.Services;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CardCollector.Tests.Pages
{
    [TestClass]
    public sealed class NewPrintingsModelTests
    {
        private Mock<ICardService> _cardServiceMock = null!;
        private NewPrintingsModel _page = null!;

        [TestMethod]
        public async Task OnGetAsync_InvalidPageSize_FallsBackToDefault()
        {
            _cardServiceMock.Setup(s => s.GetNewPrintingOpportunitiesAsync())
                .ReturnsAsync((IReadOnlyList<NewPrintingOpportunityViewModel>)[]);
            _page.PageSize = 999;

            await _page.OnGetAsync();

            Assert.AreEqual(25, _page.PageSize);
        }

        [TestMethod]
        public async Task OnGetAsync_PageNumberBelowOne_ClampsToOne()
        {
            _cardServiceMock.Setup(s => s.GetNewPrintingOpportunitiesAsync())
                .ReturnsAsync((IReadOnlyList<NewPrintingOpportunityViewModel>)[]);
            _page.PageNumber = 0;

            await _page.OnGetAsync();

            Assert.AreEqual(1, _page.PageNumber);
        }

        [TestMethod]
        public async Task OnGetAsync_PaginatesOpportunities()
        {
            var items = Enumerable.Range(1, 30).Select(i => new NewPrintingOpportunityViewModel { CardID = i }).ToList();
            _cardServiceMock.Setup(s => s.GetNewPrintingOpportunitiesAsync())
                .ReturnsAsync((IReadOnlyList<NewPrintingOpportunityViewModel>)items);
            _page.PageNumber = 2;
            _page.PageSize = 25;

            await _page.OnGetAsync();

            Assert.AreEqual(30, _page.Opportunities.TotalCount);
            Assert.AreEqual(5, _page.Opportunities.Items.Count);
        }

        [TestMethod]
        public async Task OnPostDismissAllAsync_DismissesEachPairedSetCodeAndRarityName()
        {
            _page.CardID = 1;
            _page.SetCodes = ["LOB-EN001", "LOB-EN002"];
            _page.RarityNames = ["Ultra Rare", "Secret Rare"];

            await _page.OnPostDismissAllAsync();

            _cardServiceMock.Verify(s => s.DismissNewPrintingAsync(1, "LOB-EN001", "Ultra Rare"), Times.Once);
            _cardServiceMock.Verify(s => s.DismissNewPrintingAsync(1, "LOB-EN002", "Secret Rare"), Times.Once);
        }

        [TestMethod]
        public async Task OnPostDismissAllAsync_MismatchedListLengths_StopsAtShorterList()
        {
            _page.CardID = 1;
            _page.SetCodes = ["LOB-EN001", "LOB-EN002"];
            _page.RarityNames = ["Ultra Rare"];

            await _page.OnPostDismissAllAsync();

            _cardServiceMock.Verify(s => s.DismissNewPrintingAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [TestMethod]
        public async Task OnPostDismissAsync_DismissesAndRedirects()
        {
            _page.CardID = 1;
            _page.SetCode = "LOB-EN001";
            _page.RarityName = "Ultra Rare";

            var result = await _page.OnPostDismissAsync();

            _cardServiceMock.Verify(s => s.DismissNewPrintingAsync(1, "LOB-EN001", "Ultra Rare"), Times.Once);
            Assert.IsInstanceOfType<RedirectToPageResult>(result);
        }

        [TestMethod]
        public async Task OnPostUpgradeAsync_UpgradesPreferredVersionAndRedirects()
        {
            _page.ImageID = 10;
            _page.CardID = 1;
            _page.NewSetCode = "NEW-EN001";
            _page.NewRarityName = "Secret Rare";

            var result = await _page.OnPostUpgradeAsync();

            _cardServiceMock.Verify(s => s.UpgradePreferredVersionAsync(10, 1, "NEW-EN001", "Secret Rare"), Times.Once);
            Assert.IsInstanceOfType<RedirectToPageResult>(result);
        }

        [TestInitialize]
        public void Setup()
        {
            _cardServiceMock = new Mock<ICardService>();
            _page = new NewPrintingsModel(_cardServiceMock.Object);
        }
    }
}
