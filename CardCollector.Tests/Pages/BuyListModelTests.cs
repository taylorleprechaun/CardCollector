using CardCollector.Pages;
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
    public sealed class BuyListModelTests
    {
        private Mock<ICardService> _cardServiceMock = null!;
        private Mock<IRazorPartialRenderer> _razorPartialRendererMock = null!;

        [TestMethod]
        public void GetPaginationParams_IncludesBudgetFields()
        {
            var page = CreatePage();
            page.TotalBudget = 50m;
            page.MaxCards = 10;
            page.MaxPricePerCard = 5m;

            var result = page.GetPaginationParams();

            Assert.AreEqual("50", result["totalBudget"]);
            Assert.AreEqual("10", result["maxCards"]);
            Assert.AreEqual("5", result["maxPricePerCard"]);
        }

        [TestMethod]
        public async Task OnGetAsync_BuildsMassEntryTextFromPlanItems()
        {
            _cardServiceMock.Setup(s => s.GetPurchasePlanAsync(It.IsAny<decimal?>(), It.IsAny<int?>(), It.IsAny<decimal?>(), null))
                .ReturnsAsync(new PurchasePlanViewModel
                {
                    Items =
                    [
                        new PurchasePriorityCandidateViewModel { CardName = "Dark Magician", SetCode = "LOB-EN001" }
                    ]
                });
            var page = CreatePage();

            await page.OnGetAsync();

            StringAssert.Contains(page.MassEntryText, "Dark Magician [LOB]");
        }

        [TestMethod]
        public async Task OnGetAsync_NonPositiveBudgetFields_AreNormalizedToNull()
        {
            decimal? capturedBudget = null;
            _cardServiceMock.Setup(s => s.GetPurchasePlanAsync(It.IsAny<decimal?>(), It.IsAny<int?>(), It.IsAny<decimal?>(), null))
                .Callback<decimal?, int?, decimal?, DateTime?>((budget, _, _, _) => capturedBudget = budget)
                .ReturnsAsync(new PurchasePlanViewModel());
            var page = CreatePage();
            page.TotalBudget = 0;

            await page.OnGetAsync();

            Assert.IsNull(capturedBudget);
            Assert.IsNull(page.TotalBudget);
        }

        [TestMethod]
        public async Task OnGetAsync_QueryFilter_FiltersResultsByCardName()
        {
            _cardServiceMock.Setup(s => s.GetPurchasePlanAsync(It.IsAny<decimal?>(), It.IsAny<int?>(), It.IsAny<decimal?>(), null))
                .ReturnsAsync(new PurchasePlanViewModel
                {
                    Items =
                    [
                        new PurchasePriorityCandidateViewModel { CardName = "Dark Magician", SetCode = "LOB-EN001", SetName = "Legend", RarityName = "Ultra Rare" },
                        new PurchasePriorityCandidateViewModel { CardName = "Blue-Eyes White Dragon", SetCode = "LOB-EN002", SetName = "Legend", RarityName = "Ultra Rare" }
                    ]
                });
            var page = CreatePage();
            page.Query = "Dark";

            await page.OnGetAsync();

            Assert.AreEqual(1, page.Results.TotalCount);
        }

        [TestMethod]
        public async Task OnPostAddToCartAsync_CandidateNoLongerValid_DoesNotRenderPartial()
        {
            _cardServiceMock.Setup(s => s.AddToCartAsync(1, 10, "LOB-EN001", "Ultra Rare", 3, 5m)).ReturnsAsync((0, 0m, 3));
            _cardServiceMock
                .Setup(s => s.GetPurchasePriorityCandidateAsync(1, 10, "LOB-EN001", "Ultra Rare", It.IsAny<decimal?>(), null))
                .ReturnsAsync((PurchasePriorityCandidateViewModel?)null);
            var page = CreatePage();

            await page.OnPostAddToCartAsync(1, 10, "LOB-EN001", "Ultra Rare", 3, 5m);

            _razorPartialRendererMock.Verify(r => r.RenderPartialAsync(
                It.IsAny<PageModel>(), It.IsAny<string>(), It.IsAny<object>()), Times.Never);
        }

        [TestMethod]
        public async Task OnPostAddToCartAsync_CandidateStillValid_ReturnsJsonWithRenderedRow()
        {
            _cardServiceMock.Setup(s => s.AddToCartAsync(1, 10, "LOB-EN001", "Ultra Rare", 1, 5m)).ReturnsAsync((2, 10m, 1));
            var candidate = new PurchasePriorityCandidateViewModel { CardName = "Dark Magician" };
            _cardServiceMock
                .Setup(s => s.GetPurchasePriorityCandidateAsync(1, 10, "LOB-EN001", "Ultra Rare", It.IsAny<decimal?>(), null))
                .ReturnsAsync(candidate);
            _razorPartialRendererMock
                .Setup(r => r.RenderPartialAsync(It.IsAny<PageModel>(), "_BuyListRow", candidate))
                .ReturnsAsync("<tr>row</tr>");
            var page = CreatePage();

            var result = await page.OnPostAddToCartAsync(1, 10, "LOB-EN001", "Ultra Rare", 1, 5m) as JsonResult;

            Assert.IsNotNull(result);
        }

        [TestMethod]
        public async Task OnPostAddToCartAsync_InvalidParams_ReturnsBadRequest()
        {
            var page = CreatePage();

            var result = await page.OnPostAddToCartAsync(0, 10, "LOB-EN001", "Ultra Rare", 1, null);

            Assert.IsInstanceOfType<BadRequestResult>(result);
        }

        [TestInitialize]
        public void Setup()
        {
            _cardServiceMock = new Mock<ICardService>();
            _razorPartialRendererMock = new Mock<IRazorPartialRenderer>();
        }

        private BuyListModel CreatePage()
        {
            var page = new BuyListModel(_cardServiceMock.Object, _razorPartialRendererMock.Object);
            PageContextFactory.Attach(page);
            return page;
        }
    }
}
