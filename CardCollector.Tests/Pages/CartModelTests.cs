using CardCollector.Data.Models;
using CardCollector.Pages;
using CardCollector.Repository;
using CardCollector.Services;
using CardCollector.Tests.TestHelpers;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CardCollector.Tests.Pages
{
    [TestClass]
    public sealed class CartModelTests
    {
        private Mock<ICardService> _cardServiceMock = null!;
        private Mock<IPendingOrderRepository> _pendingOrderRepositoryMock = null!;

        [TestMethod]
        public async Task OnGetAsync_PopulatesLinesFromCardService()
        {
            _cardServiceMock.Setup(s => s.GetPendingCartAsync())
                .ReturnsAsync((IReadOnlyList<PendingOrderLineViewModel>)[new PendingOrderLineViewModel { CardName = "Dark Magician" }]);
            var page = CreatePage();

            await page.OnGetAsync();

            Assert.AreEqual(1, page.Lines.Count);
        }

        [TestMethod]
        public async Task OnPostRemoveAsync_DeletesLineAndRedirects()
        {
            var page = CreatePage();

            var result = await page.OnPostRemoveAsync(5);

            _pendingOrderRepositoryMock.Verify(r => r.DeleteAsync(5), Times.Once);
            Assert.IsInstanceOfType<RedirectToPageResult>(result);
        }

        [TestMethod]
        public async Task OnPostSubmitAllAsync_DuplicateLineIDs_ReturnsBadRequest()
        {
            var page = CreatePage();
            var lines = new List<CartLineOverride>
            {
                new() { PendingOrderLineID = 1, PurchasePrice = 5m },
                new() { PendingOrderLineID = 1, PurchasePrice = 6m }
            };

            var result = await page.OnPostSubmitAllAsync(lines);

            Assert.IsInstanceOfType<BadRequestResult>(result);
        }

        [TestMethod]
        public async Task OnPostSubmitAllAsync_EditionWarningsPresent_SetsWarningTempData()
        {
            var page = CreatePage();
            var lines = new List<CartLineOverride> { new() { PendingOrderLineID = 1, PurchasePrice = 5m } };
            _cardServiceMock.Setup(s => s.SubmitCartAsync(lines)).ReturnsAsync((1, 5m, (IReadOnlyList<string>)["Edition mismatch warning."]));

            await page.OnPostSubmitAllAsync(lines);

            Assert.AreEqual("Edition mismatch warning.", page.TempData["Warning"]);
        }

        [TestMethod]
        public async Task OnPostSubmitAllAsync_EmptyCartSubmission_SetsEmptyCartMessage()
        {
            var page = CreatePage();
            var lines = new List<CartLineOverride> { new() { PendingOrderLineID = 1, PurchasePrice = 5m } };
            _cardServiceMock.Setup(s => s.SubmitCartAsync(lines)).ReturnsAsync((0, 0m, (IReadOnlyList<string>)[]));

            var result = await page.OnPostSubmitAllAsync(lines);

            Assert.AreEqual("Your cart is empty — nothing to submit.", page.TempData["Success"]);
            var redirect = result as RedirectToPageResult;
            Assert.AreEqual("/Orders", redirect!.PageName);
        }

        [TestMethod]
        public async Task OnPostSubmitAllAsync_MissingPurchasePrice_SetsWarningAndRedirects()
        {
            var page = CreatePage();
            var lines = new List<CartLineOverride> { new() { PendingOrderLineID = 1, PurchasePrice = null } };

            var result = await page.OnPostSubmitAllAsync(lines);

            Assert.AreEqual("Enter a price for every cart line before submitting.", page.TempData["Warning"]);
            Assert.IsInstanceOfType<RedirectToPageResult>(result);
        }

        [TestMethod]
        public async Task OnPostSubmitAllAsync_NullLines_ReturnsBadRequest()
        {
            var page = CreatePage();

            var result = await page.OnPostSubmitAllAsync(null);

            Assert.IsInstanceOfType<BadRequestResult>(result);
        }

        [TestMethod]
        public async Task OnPostSubmitAllAsync_SuccessfulSubmission_SetsSuccessMessageWithCount()
        {
            var page = CreatePage();
            var lines = new List<CartLineOverride> { new() { PendingOrderLineID = 1, PurchasePrice = 5m } };
            _cardServiceMock.Setup(s => s.SubmitCartAsync(lines)).ReturnsAsync((1, 5m, (IReadOnlyList<string>)[]));

            await page.OnPostSubmitAllAsync(lines);

            Assert.AreEqual("Added 1 order for $5.00 to Orders.", page.TempData["Success"]);
        }

        [TestMethod]
        public async Task OnPostUpdateQuantityAsync_NoSuchLine_ReturnsNotFound()
        {
            _cardServiceMock.Setup(s => s.UpdateCartLineQuantityAsync(999, 2)).ReturnsAsync(false);
            var page = CreatePage();

            var result = await page.OnPostUpdateQuantityAsync(999, 2);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task OnPostUpdateQuantityAsync_UpdateSucceeds_ReturnsOk()
        {
            _cardServiceMock.Setup(s => s.UpdateCartLineQuantityAsync(1, 2)).ReturnsAsync(true);
            var page = CreatePage();

            var result = await page.OnPostUpdateQuantityAsync(1, 2);

            Assert.IsInstanceOfType<OkResult>(result);
        }

        [TestInitialize]
        public void Setup()
        {
            _cardServiceMock = new Mock<ICardService>();
            _pendingOrderRepositoryMock = new Mock<IPendingOrderRepository>();
        }

        private CartModel CreatePage()
        {
            var page = new CartModel(_cardServiceMock.Object, _pendingOrderRepositoryMock.Object);
            PageContextFactory.Attach(page);
            return page;
        }
    }
}
