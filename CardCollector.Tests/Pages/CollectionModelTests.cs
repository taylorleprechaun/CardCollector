using CardCollector.Data.Models;
using CardCollector.DTO;
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
    public sealed class CollectionModelTests
    {
        private Mock<ICardDataRepository> _cardDataRepositoryMock = null!;
        private Mock<ICardService> _cardServiceMock = null!;
        private Mock<ICardSetRepository> _cardSetRepositoryMock = null!;
        private Mock<ICollectionRepository> _collectionRepositoryMock = null!;
        private Mock<IRazorPartialRenderer> _razorPartialRendererMock = null!;

        [TestMethod]
        public void ActiveFilterCount_ConditionEditionAcquisitionCheckedOutSet_AddsFourToBaseCount()
        {
            var page = CreatePage();
            page.Condition = CardCondition.NearMint;
            page.Edition = CardEdition.FirstEdition;
            page.AcquisitionMethod = AcquisitionMethod.Purchased;
            page.CheckedOutFilter = "yes";

            Assert.AreEqual(4, page.ActiveFilterCount);
        }

        [TestMethod]
        public void GetTCGDate_DelegatesToCardSetRepository()
        {
            _cardSetRepositoryMock.Setup(r => r.GetTCGDateBySetCode("LOB-EN001")).Returns("2002-03-08");
            var page = CreatePage();

            Assert.AreEqual("2002-03-08", page.GetTCGDate("LOB-EN001"));
        }

        [TestMethod]
        public void HasActiveFilters_ConditionSet_ReturnsTrue()
        {
            var page = CreatePage();
            page.Condition = CardCondition.Damaged;

            Assert.IsTrue(page.HasActiveFilters);
        }

        [TestMethod]
        public async Task OnGetAsync_PopulatesAvailableFiltersAndGroupedCards()
        {
            _collectionRepositoryMock.Setup(r => r.GetDistinctSetCodesAsync()).ReturnsAsync((IReadOnlyList<string>)["LOB-EN001"]);
            _cardServiceMock.Setup(s => s.SearchGroupedOwnedAsync(It.IsAny<CollectionSearchCriteria>()))
                .ReturnsAsync(new PagedResult<CollectionGroupViewModel> { TotalCount = 7 });
            var page = CreatePage();

            await page.OnGetAsync();

            Assert.AreEqual(7, page.GroupedCards.TotalCount);
            CollectionAssert.AreEqual(new[] { "LOB-EN001" }, page.AvailableSetNames.ToArray());
        }

        [TestMethod]
        public async Task OnGetAsync_SetCodeHasCanonicalName_UsesCanonicalNameInsteadOfCode()
        {
            _collectionRepositoryMock.Setup(r => r.GetDistinctSetCodesAsync()).ReturnsAsync((IReadOnlyList<string>)["LOB-EN001"]);
            _cardDataRepositoryMock.Setup(r => r.GetSetNamesByCode())
                .Returns(new Dictionary<string, string> { ["LOB-EN001"] = "Legend of Blue Eyes White Dragon" });
            _cardServiceMock.Setup(s => s.SearchGroupedOwnedAsync(It.IsAny<CollectionSearchCriteria>()))
                .ReturnsAsync(new PagedResult<CollectionGroupViewModel>());
            var page = CreatePage();

            await page.OnGetAsync();

            CollectionAssert.AreEqual(new[] { "Legend of Blue Eyes White Dragon" }, page.AvailableSetNames.ToArray());
        }

        [TestMethod]
        public async Task OnPostAddPurchaseAsync_AddsOwnedEntryAndRedirectsWhenNotAjax()
        {
            _cardServiceMock.Setup(s => s.SearchGroupedOwnedAsync(It.IsAny<CollectionSearchCriteria>()))
                .ReturnsAsync(new PagedResult<CollectionGroupViewModel>());
            var page = CreatePage(isAjax: false);

            var result = await page.OnPostAddPurchaseAsync(1, 10, "LOB-EN001", 2, null, null, null, null, null, null);

            _cardServiceMock.Verify(s => s.AddEntryAsync(
                1, 10, "LOB-EN001", CollectionStatus.Owned, 2,
                null, null, null, null, null, null, null), Times.Once);
            Assert.IsInstanceOfType<RedirectToPageResult>(result);
        }

        [TestMethod]
        public async Task OnPostAddPurchaseAsync_SetAsPreferredTrue_AlsoSavesPreferredVersion()
        {
            _cardServiceMock.Setup(s => s.SearchGroupedOwnedAsync(It.IsAny<CollectionSearchCriteria>()))
                .ReturnsAsync(new PagedResult<CollectionGroupViewModel>());
            var page = CreatePage(isAjax: false);

            await page.OnPostAddPurchaseAsync(1, 10, "LOB-EN001", 2, null, null, null, null, null, null, setAsPreferred: true);

            _cardServiceMock.Verify(s => s.SavePreferredVersionAsync(1, 10, "LOB-EN001", null), Times.Once);
        }

        [TestMethod]
        public async Task OnPostCheckInAsync_AjaxWithNoMatchingGroup_ReturnsEmptyContent()
        {
            _cardServiceMock.Setup(s => s.SearchGroupedOwnedAsync(It.IsAny<CollectionSearchCriteria>()))
                .ReturnsAsync(new PagedResult<CollectionGroupViewModel>());
            var page = CreatePage(isAjax: true);

            var result = await page.OnPostCheckInAsync(10, "LOB-EN001", "Ultra Rare") as ContentResult;

            Assert.AreEqual(string.Empty, result!.Content);
        }

        [TestMethod]
        public async Task OnPostCheckInAsync_ChecksInCardAndRedirectsWhenNotAjax()
        {
            _cardServiceMock.Setup(s => s.SearchGroupedOwnedAsync(It.IsAny<CollectionSearchCriteria>()))
                .ReturnsAsync(new PagedResult<CollectionGroupViewModel>());
            var page = CreatePage(isAjax: false);

            var result = await page.OnPostCheckInAsync(10, "LOB-EN001", "Ultra Rare");

            _cardServiceMock.Verify(s => s.CheckInCardAsync(10, "LOB-EN001", "Ultra Rare"), Times.Once);
            Assert.IsInstanceOfType<RedirectToPageResult>(result);
        }

        [TestMethod]
        public async Task OnPostCheckOutAsync_QuantityIsPositive_ChecksOutCard()
        {
            _cardServiceMock.Setup(s => s.SearchGroupedOwnedAsync(It.IsAny<CollectionSearchCriteria>()))
                .ReturnsAsync(new PagedResult<CollectionGroupViewModel>());
            var page = CreatePage(isAjax: false);

            await page.OnPostCheckOutAsync(1, 10, "LOB-EN001", "Ultra Rare", 3);

            _cardServiceMock.Verify(s => s.CheckOutCardAsync(1, 10, "LOB-EN001", "Ultra Rare", 3), Times.Once);
        }

        [TestMethod]
        public async Task OnPostCheckOutAsync_QuantityIsZero_DoesNotCheckOutCard()
        {
            _cardServiceMock.Setup(s => s.SearchGroupedOwnedAsync(It.IsAny<CollectionSearchCriteria>()))
                .ReturnsAsync(new PagedResult<CollectionGroupViewModel>());
            var page = CreatePage(isAjax: false);

            await page.OnPostCheckOutAsync(1, 10, "LOB-EN001", "Ultra Rare", 0);

            _cardServiceMock.Verify(s => s.CheckOutCardAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        }

        [TestMethod]
        public async Task OnPostDeleteAsync_AjaxWithMatch_ReturnsRenderedPartialAndSetsHeader()
        {
            _collectionRepositoryMock.Setup(r => r.GetByIDAsync(1))
                .ReturnsAsync(new CollectionEntry { ID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = "Ultra Rare" });
            _cardServiceMock.Setup(s => s.SearchGroupedOwnedAsync(It.IsAny<CollectionSearchCriteria>()))
                .ReturnsAsync(new PagedResult<CollectionGroupViewModel>
                {
                    TotalCount = 1,
                    Items = [new CollectionGroupViewModel { ImageID = 10, SetCode = "LOB-EN001", RarityName = "Ultra Rare" }]
                });
            _razorPartialRendererMock
                .Setup(r => r.RenderPartialAsync(It.IsAny<PageModel>(), "_CollectionGroupRow", It.IsAny<CollectionGroupRowViewModel>()))
                .ReturnsAsync("<tr>row</tr>");
            var page = CreatePage(isAjax: true);

            var result = await page.OnPostDeleteAsync(1) as ContentResult;

            Assert.AreEqual("<tr>row</tr>", result!.Content);
            Assert.AreEqual("1", (string?)page.HttpContext.Response.Headers["X-Total-Count"]);
        }

        [TestMethod]
        public async Task OnPostDeleteAsync_NoExistingEntry_StillRespondsWithoutThrowing()
        {
            _collectionRepositoryMock.Setup(r => r.GetByIDAsync(999)).ReturnsAsync((CollectionEntry?)null);
            _cardServiceMock.Setup(s => s.SearchGroupedOwnedAsync(It.IsAny<CollectionSearchCriteria>()))
                .ReturnsAsync(new PagedResult<CollectionGroupViewModel>());
            var page = CreatePage(isAjax: false);

            var result = await page.OnPostDeleteAsync(999);

            _collectionRepositoryMock.Verify(r => r.DeleteAsync(999), Times.Once);
            Assert.IsInstanceOfType<RedirectToPageResult>(result);
        }

        [TestMethod]
        public async Task OnPostEditAsync_NoExistingEntry_StillRespondsWithoutThrowing()
        {
            _collectionRepositoryMock.Setup(r => r.GetByIDAsync(999)).ReturnsAsync((CollectionEntry?)null);
            _cardServiceMock.Setup(s => s.SearchGroupedOwnedAsync(It.IsAny<CollectionSearchCriteria>()))
                .ReturnsAsync(new PagedResult<CollectionGroupViewModel>());
            var page = CreatePage(isAjax: false);

            var result = await page.OnPostEditAsync(999, 1, null, null, null, null, null, null);

            Assert.IsInstanceOfType<RedirectToPageResult>(result);
        }

        [TestMethod]
        public async Task OnPostEditAsync_QuantityBelowOne_ClampsToOne()
        {
            _collectionRepositoryMock.Setup(r => r.GetByIDAsync(1)).ReturnsAsync(new CollectionEntry { ID = 1, ImageID = 10, SetCode = "LOB-EN001" });
            CollectionEntry? captured = null;
            _collectionRepositoryMock
                .Setup(r => r.UpdateAsync(It.IsAny<CollectionEntry>()))
                .Callback<CollectionEntry>(e => captured = e)
                .ReturnsAsync(true);
            _cardServiceMock.Setup(s => s.SearchGroupedOwnedAsync(It.IsAny<CollectionSearchCriteria>()))
                .ReturnsAsync(new PagedResult<CollectionGroupViewModel>());
            var page = CreatePage(isAjax: false);

            await page.OnPostEditAsync(1, 0, null, null, null, null, null, null);

            Assert.AreEqual(1, captured!.Quantity);
        }

        [TestInitialize]
        public void Setup()
        {
            _cardDataRepositoryMock = new Mock<ICardDataRepository>();
            _cardDataRepositoryMock.Setup(r => r.GetSetNamesByCode()).Returns(new Dictionary<string, string>());
            _cardServiceMock = new Mock<ICardService>();
            _cardSetRepositoryMock = new Mock<ICardSetRepository>();
            _collectionRepositoryMock = new Mock<ICollectionRepository>();
            _collectionRepositoryMock.Setup(r => r.GetDistinctSetCodesAsync()).ReturnsAsync((IReadOnlyList<string>)[]);
            _collectionRepositoryMock.Setup(r => r.GetDistinctRarityNamesAsync()).ReturnsAsync((IReadOnlyList<string>)[]);
            _collectionRepositoryMock.Setup(r => r.GetDistinctConditionsAsync()).ReturnsAsync((IReadOnlyList<CardCondition>)[]);
            _collectionRepositoryMock.Setup(r => r.GetDistinctEditionsAsync()).ReturnsAsync((IReadOnlyList<CardEdition>)[]);
            _collectionRepositoryMock.Setup(r => r.GetDistinctAcquisitionMethodsAsync()).ReturnsAsync((IReadOnlyList<AcquisitionMethod>)[]);
            _razorPartialRendererMock = new Mock<IRazorPartialRenderer>();
        }

        private CollectionModel CreatePage(bool isAjax = false)
        {
            var page = new CollectionModel(
                _cardDataRepositoryMock.Object, _cardServiceMock.Object, _cardSetRepositoryMock.Object,
                _collectionRepositoryMock.Object, _razorPartialRendererMock.Object);
            PageContextFactory.Attach(page, httpContext =>
            {
                if (isAjax)
                    httpContext.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
            });
            return page;
        }
    }
}
