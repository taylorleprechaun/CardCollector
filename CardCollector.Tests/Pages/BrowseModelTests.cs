using CardCollector.Pages;
using CardCollector.Repository;
using CardCollector.Services;
using CardCollector.Tests.TestHelpers;
using CardCollector.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CardCollector.Tests.Pages
{
    [TestClass]
    public sealed class BrowseModelTests
    {
        private Mock<ICardDataRepository> _cardDataRepositoryMock = null!;
        private Mock<ICardService> _cardServiceMock = null!;
        [TestMethod]
        public void ActiveFilterCount_CollectionOrderedWishlistFiltersSet_AddsToBaseCount()
        {
            var page = CreatePage();
            page.CollectionFilter = "yes";
            page.OrderedFilter = "yes";
            page.WishlistFilter = "yes";

            Assert.AreEqual(3, page.ActiveFilterCount);
        }

        [TestMethod]
        public void GetPaginationParams_IncludesAllThreeFilterKeys()
        {
            var page = CreatePage();
            page.CollectionFilter = "yes";
            page.OrderedFilter = "no";
            page.WishlistFilter = "yes";

            var result = page.GetPaginationParams();

            Assert.AreEqual("yes", result["collectionFilter"]);
            Assert.AreEqual("no", result["orderedFilter"]);
            Assert.AreEqual("yes", result["wishlistFilter"]);
        }

        [TestMethod]
        public void HasActiveFilters_AnyOfTheThreeFiltersSet_ReturnsTrue()
        {
            var page = CreatePage();
            page.WishlistFilter = "yes";

            Assert.IsTrue(page.HasActiveFilters);
        }

        [TestMethod]
        public async Task OnGetAsync_CollectionFilterIsIncomplete_SetsIsIncompleteCriteriaFlag()
        {
            BrowseSearchCriteria? captured = null;
            _cardServiceMock.Setup(s => s.SearchCardsAsync(It.IsAny<BrowseSearchCriteria>()))
                .Callback<BrowseSearchCriteria>(c => captured = c)
                .ReturnsAsync(new PagedResult<CardListItemViewModel>());
            var page = CreatePage();
            page.CollectionFilter = "incomplete";

            await page.OnGetAsync();

            Assert.IsTrue(captured!.IsIncomplete);
            Assert.IsTrue(captured.InCollection);
        }

        [TestMethod]
        [DataRow("yes", true)]
        [DataRow("no", false)]
        [DataRow("other", null)]
        public async Task OnGetAsync_OrderedFilterParsesYesNoOtherwiseNull(string filterValue, bool? expected)
        {
            BrowseSearchCriteria? captured = null;
            _cardServiceMock.Setup(s => s.SearchCardsAsync(It.IsAny<BrowseSearchCriteria>()))
                .Callback<BrowseSearchCriteria>(c => captured = c)
                .ReturnsAsync(new PagedResult<CardListItemViewModel>());
            var page = CreatePage();
            page.OrderedFilter = filterValue;

            await page.OnGetAsync();

            Assert.AreEqual(expected, captured!.IsOrdered);
        }

        [TestMethod]
        public async Task OnGetAsync_PopulatesAvailableFiltersAndResults()
        {
            _cardServiceMock.Setup(s => s.SearchCardsAsync(It.IsAny<BrowseSearchCriteria>()))
                .ReturnsAsync(new PagedResult<CardListItemViewModel> { TotalCount = 5 });
            var page = CreatePage();

            await page.OnGetAsync();

            CollectionAssert.AreEqual(new[] { "Ultra Rare" }, page.AvailableRarityNames.ToArray());
            Assert.AreEqual(5, page.Results.TotalCount);
        }

        [TestInitialize]
        public void Setup()
        {
            _cardServiceMock = new Mock<ICardService>();
            _cardDataRepositoryMock = new Mock<ICardDataRepository>();
            _cardDataRepositoryMock.Setup(r => r.DistinctRarityNames).Returns(["Ultra Rare"]);
            _cardDataRepositoryMock.Setup(r => r.DistinctSetNames).Returns(["Legend of Blue Eyes White Dragon"]);
        }

        private BrowseModel CreatePage()
        {
            var page = new BrowseModel(_cardServiceMock.Object, _cardDataRepositoryMock.Object);
            PageContextFactory.Attach(page);
            return page;
        }
    }
}
