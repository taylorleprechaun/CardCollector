using CardCollector.Data.Models;
using CardCollector.Extensions;
using CardCollector.Services;
using CardCollector.Tests.TestHelpers;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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

        [TestInitialize]
        public void Setup()
        {
            _cardServiceMock = new Mock<ICardService>();
        }

        [TestMethod]
        public async Task WarnIfEditionMismatchAsync_CategoryIsEditionMismatch_SetsTempDataWarning()
        {
            var page = PageContextFactory.Create<TestPageModel>();
            _cardServiceMock
                .Setup(s => s.CheckEntryEditionAsync(1, "LOB-EN124", "Ultra Rare", CardEdition.FirstEdition))
                .ReturnsAsync(EditionAuditCategory.EditionMismatch);

            await page.WarnIfEditionMismatchAsync(_cardServiceMock.Object, 1, "LOB-EN124", "Ultra Rare", CardEdition.FirstEdition);

            Assert.AreEqual(
                "TCGPlayer doesn't list a 1st Edition printing of LOB-EN124 Ultra Rare for this card — double-check the edition you selected.",
                page.TempData["Warning"]);
        }

        [TestMethod]
        public async Task WarnIfEditionMismatchAsync_CategoryIsNull_DoesNotSetTempData()
        {
            var page = PageContextFactory.Create<TestPageModel>();
            _cardServiceMock
                .Setup(s => s.CheckEntryEditionAsync(1, "LOB-EN124", "Ultra Rare", CardEdition.FirstEdition))
                .ReturnsAsync((EditionAuditCategory?)null);

            await page.WarnIfEditionMismatchAsync(_cardServiceMock.Object, 1, "LOB-EN124", "Ultra Rare", CardEdition.FirstEdition);

            Assert.IsFalse(page.TempData.ContainsKey("Warning"));
        }

        [TestMethod]
        public async Task WarnIfEditionMismatchAsync_CategoryIsUnverifiable_DoesNotSetTempData()
        {
            var page = PageContextFactory.Create<TestPageModel>();
            _cardServiceMock
                .Setup(s => s.CheckEntryEditionAsync(1, "LOB-EN124", "Ultra Rare", CardEdition.FirstEdition))
                .ReturnsAsync(EditionAuditCategory.Unverifiable);

            await page.WarnIfEditionMismatchAsync(_cardServiceMock.Object, 1, "LOB-EN124", "Ultra Rare", CardEdition.FirstEdition);

            Assert.IsFalse(page.TempData.ContainsKey("Warning"));
        }

        [TestMethod]
        public async Task WarnIfEditionMismatchAsync_EditionIsNull_DoesNotCallCardServiceOrSetTempData()
        {
            var page = PageContextFactory.Create<TestPageModel>();

            await page.WarnIfEditionMismatchAsync(_cardServiceMock.Object, 1, "LOB-EN124", "Ultra Rare", null);

            _cardServiceMock.Verify(s => s.CheckEntryEditionAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CardEdition>()), Times.Never);
            Assert.IsFalse(page.TempData.ContainsKey("Warning"));
        }

        [TestMethod]
        [DataRow(null, DisplayName = "Null rarity name")]
        [DataRow("", DisplayName = "Empty rarity name")]
        [DataRow("   ", DisplayName = "Whitespace rarity name")]
        public async Task WarnIfEditionMismatchAsync_RarityNameIsBlank_DoesNotCallCardServiceOrSetTempData(string? rarityName)
        {
            var page = PageContextFactory.Create<TestPageModel>();

            await page.WarnIfEditionMismatchAsync(_cardServiceMock.Object, 1, "LOB-EN124", rarityName, CardEdition.FirstEdition);

            _cardServiceMock.Verify(s => s.CheckEntryEditionAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CardEdition>()), Times.Never);
            Assert.IsFalse(page.TempData.ContainsKey("Warning"));
        }

        private sealed class TestPageModel : PageModel
        {
        }
    }
}
