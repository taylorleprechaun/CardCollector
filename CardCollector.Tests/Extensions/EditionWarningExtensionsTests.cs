using CardCollector.Data.Models;
using CardCollector.DTO;
using CardCollector.Extensions;
using CardCollector.Models.Catalog;
using CardCollector.Services;
using CardCollector.Tests.TestHelpers;
using CardCollector.ViewModels;
using Moq;

namespace CardCollector.Tests.Extensions
{
    [TestClass]
    public sealed class EditionWarningExtensionsTests
    {
        private Mock<ICardService> _cardServiceMock = null!;

        [TestMethod]
        public void BuildEditionMismatchMessage_ReturnsExpectedMessage()
        {
            var result = EditionWarningExtensions.BuildEditionMismatchMessage(CardEdition.FirstEdition, "LOB-EN124", "Ultra Rare");

            Assert.AreEqual(
                "TCGPlayer doesn't list a 1st Edition printing of LOB-EN124 Ultra Rare for this card — double-check the edition you selected.",
                result);
        }

        [TestMethod]
        public async Task WarnIfEditionMismatchAsync_CategoryIsEditionMismatch_SetsWarningNamingThePrinting()
        {
            var page = PageContextFactory.Create<TestPageModel>();
            SetUpPrinting();
            _cardServiceMock.Setup(s => s.CheckEntryEditionAsync(101, CardEdition.FirstEdition)).ReturnsAsync(EditionAuditCategory.EditionMismatch);

            await page.WarnIfEditionMismatchAsync(_cardServiceMock.Object, 101, CardEdition.FirstEdition);

            Assert.AreEqual(
                "TCGPlayer doesn't list a 1st Edition printing of LOB-EN124 Ultra Rare for this card — double-check the edition you selected.",
                page.TempData["Warning"]);
        }

        [TestMethod]
        public async Task WarnIfEditionMismatchAsync_EditionIsNull_DoesNotCheckOrSetTempData()
        {
            var page = PageContextFactory.Create<TestPageModel>();

            await page.WarnIfEditionMismatchAsync(_cardServiceMock.Object, 101, null);

            _cardServiceMock.Verify(s => s.CheckEntryEditionAsync(It.IsAny<int>(), It.IsAny<CardEdition>()), Times.Never);
            Assert.IsFalse(page.TempData.ContainsKey("Warning"));
        }

        [TestMethod]
        [DataRow(null, DisplayName = "Edition listed")]
        [DataRow(EditionAuditCategory.Unverifiable, DisplayName = "No linked product")]
        public async Task WarnIfEditionMismatchAsync_NotAMismatch_DoesNotSetTempData(EditionAuditCategory? category)
        {
            var page = PageContextFactory.Create<TestPageModel>();
            SetUpPrinting();
            _cardServiceMock.Setup(s => s.CheckEntryEditionAsync(101, CardEdition.FirstEdition)).ReturnsAsync(category);

            await page.WarnIfEditionMismatchAsync(_cardServiceMock.Object, 101, CardEdition.FirstEdition);

            Assert.IsFalse(page.TempData.ContainsKey("Warning"));
        }

        [TestInitialize]
        public void Setup()
        {
            _cardServiceMock = new Mock<ICardService>();
        }

        private void SetUpPrinting()
        {
            var set = new Set { Code = "LOB-EN124", PrintingID = 101, RarityName = "Ultra Rare" };
            _cardServiceMock.Setup(s => s.GetPrintingByID(101)).Returns(new CatalogPrintingRef(new Card { ID = 1, CardSets = [set] }, set));
        }
    }
}
