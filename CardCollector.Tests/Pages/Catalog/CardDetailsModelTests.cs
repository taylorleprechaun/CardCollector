using CardCollector.Data.Models.Catalog;
using CardCollector.Models;
using CardCollector.Pages.Catalog;
using CardCollector.Services;
using CardCollector.Tests.TestHelpers;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Moq;

namespace CardCollector.Tests.Pages.Catalog
{
    [TestClass]
    public sealed class CardDetailsModelTests
    {
        private Mock<ICatalogAdminService> _catalogAdminServiceMock = null!;

        [TestMethod]
        public async Task OnGetAsync_CardExists_FillsFormFromCard()
        {
            var fields = new CatalogCardEditInput { Name = "Synthetic Theory" };
            _catalogAdminServiceMock.Setup(s => s.GetCardAsync(7, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CatalogCardDetailsViewModel { Fields = fields, ID = 7, Name = "Synthetic Theory" });
            var page = CreatePage(7);

            var result = await page.OnGetAsync();

            Assert.IsInstanceOfType<PageResult>(result);
            Assert.AreSame(fields, page.Input);
        }

        [TestMethod]
        public async Task OnGetAsync_CardMissing_ReturnsNotFound()
        {
            var page = CreatePage(7);

            var result = await page.OnGetAsync();

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task OnPostMergeAsync_Failure_RedirectsToPrintingsWithWarning()
        {
            _catalogAdminServiceMock.Setup(s => s.MergePrintingAsync(10, 11, It.IsAny<CancellationToken>()))
                .ReturnsAsync(SaveResult.Failure(["Printings can only be merged within the same card."]));
            var page = CreatePage(7);

            var result = await page.OnPostMergeAsync(10, 11) as RedirectToPageResult;

            Assert.AreEqual("printings", result!.Fragment);
            Assert.AreEqual(7, result.RouteValues!["id"]);
            Assert.AreEqual("Printings can only be merged within the same card.", page.TempData["Warning"]);
        }

        [TestMethod]
        public async Task OnPostPrintingStatusAsync_Retired_RedirectsWithSuccess()
        {
            _catalogAdminServiceMock.Setup(s => s.SetPrintingStatusAsync(10, CatalogStatus.Retired, It.IsAny<CancellationToken>()))
                .ReturnsAsync(SaveResult.Success());
            var page = CreatePage(7);

            var result = await page.OnPostPrintingStatusAsync(10, CatalogStatus.Retired);

            Assert.IsInstanceOfType<RedirectToPageResult>(result);
            Assert.AreEqual("Printing retired.", page.TempData["Success"]);
        }

        [TestMethod]
        public async Task OnPostSaveAsync_Refused_RerendersPageKeepingTypedValues()
        {
            var typed = new CatalogCardEditInput { Name = "Synthetic Theory", Passcode = 74733322 };
            _catalogAdminServiceMock.Setup(s => s.UpdateCardAsync(7, typed, It.IsAny<CancellationToken>()))
                .ReturnsAsync(SaveResult.Failure(["Passcode 74733322 already belongs to \"Synthetic Artmage\"."]));
            _catalogAdminServiceMock.Setup(s => s.GetCardAsync(7, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CatalogCardDetailsViewModel { ID = 7, Name = "Synthetic Theory" });
            var page = CreatePage(7);
            page.Input = typed;

            var result = await page.OnPostSaveAsync();

            Assert.IsInstanceOfType<PageResult>(result);
            Assert.AreSame(typed, page.Input);
            Assert.AreEqual("Passcode 74733322 already belongs to \"Synthetic Artmage\".", page.EditError);
        }

        [TestMethod]
        public async Task OnPostSaveAsync_Saved_RedirectsWithSuccess()
        {
            _catalogAdminServiceMock.Setup(s => s.UpdateCardAsync(7, It.IsAny<CatalogCardEditInput>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(SaveResult.Success());
            var page = CreatePage(7);

            var result = await page.OnPostSaveAsync();

            Assert.IsInstanceOfType<RedirectToPageResult>(result);
            Assert.IsNotNull(page.TempData["Success"]);
        }

        [TestInitialize]
        public void Setup()
        {
            _catalogAdminServiceMock = new Mock<ICatalogAdminService>();
        }

        private CardDetailsModel CreatePage(int id)
        {
            var page = new CardDetailsModel(_catalogAdminServiceMock.Object) { ID = id };
            PageContextFactory.Attach(page);
            return page;
        }
    }
}
