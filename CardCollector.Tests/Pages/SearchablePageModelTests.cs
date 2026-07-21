using CardCollector.Pages;
using CardCollector.Services;
using CardCollector.Tests.TestHelpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CardCollector.Tests.Pages
{
    [TestClass]
    public sealed class SearchablePageModelTests
    {
        private Mock<ICardService> _cardServiceMock = null!;

        private TestSearchablePageModel _page = null!;

        [TestMethod]
        public void ActiveFilterCount_AllThreeFiltersSet_ReturnsThree()
        {
            _page.CardType = "Monster";
            _page.RarityName = "Ultra Rare";
            _page.SetName = "Legend of Blue Eyes White Dragon";

            Assert.AreEqual(3, _page.ActiveFilterCount);
        }

        [TestMethod]
        public void ActiveFilterCount_NoFiltersSet_ReturnsZero()
        {
            Assert.AreEqual(0, _page.ActiveFilterCount);
        }

        [TestMethod]
        public void HasActiveFilters_NoFiltersSet_ReturnsFalse()
        {
            Assert.IsFalse(_page.HasActiveFilters);
        }

        [TestMethod]
        public void HasActiveFilters_OneFilterSet_ReturnsTrue()
        {
            _page.RarityName = "Ultra Rare";

            Assert.IsTrue(_page.HasActiveFilters);
        }

        [TestMethod]
        [DataRow(0, 1, DisplayName = "Zero clamps to one")]
        [DataRow(-5, 1, DisplayName = "Negative clamps to one")]
        [DataRow(3, 3, DisplayName = "Valid value passes through")]
        public void NormalizeSearchParameters_PageNumber_ClampsBelowOneToOne(int input, int expected)
        {
            _page.PageNumber = input;

            _page.CallNormalizeSearchParameters();

            Assert.AreEqual(expected, _page.PageNumber);
        }

        [TestMethod]
        [DataRow(15, 25, DisplayName = "Invalid size falls back to 25")]
        [DataRow(50, 50, DisplayName = "Valid size passes through")]
        public void NormalizeSearchParameters_PageSize_FallsBackToDefaultWhenInvalid(int input, int expected)
        {
            _page.PageSize = input;

            _page.CallNormalizeSearchParameters();

            Assert.AreEqual(expected, _page.PageSize);
        }

        [TestMethod]
        public void OnGetAutocomplete_BlankQuery_ReturnsEmptyArrayWithoutCallingCardService()
        {
            var result = _page.OnGetAutocomplete("   ") as JsonResult;

            CollectionAssert.AreEqual(Array.Empty<string>(), (string[])result!.Value!);
            _cardServiceMock.Verify(s => s.GetCardNameSuggestions(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        }

        [TestMethod]
        public void OnGetAutocomplete_NonBlankQuery_DelegatesToCardService()
        {
            _cardServiceMock.Setup(s => s.GetCardNameSuggestions("dark", 10)).Returns(["Dark Magician"]);

            var result = _page.OnGetAutocomplete("dark") as JsonResult;

            CollectionAssert.AreEqual(new[] { "Dark Magician" }, ((IEnumerable<string>)result!.Value!).ToArray());
        }

        [TestInitialize]
        public void Setup()
        {
            _cardServiceMock = new Mock<ICardService>();
            _page = PageContextFactory.Create<TestSearchablePageModel>();
            _page.CardServiceOverride = _cardServiceMock.Object;
        }

        private sealed class TestSearchablePageModel : SearchablePageModel
        {
            public ICardService? CardServiceOverride { get; set; }
            protected override ICardService CardService => CardServiceOverride!;
            public void CallNormalizeSearchParameters() => NormalizeSearchParameters();
        }
    }
}
