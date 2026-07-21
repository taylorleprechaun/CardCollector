using CardCollector.Data.Models;
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
    public sealed class OrdersModelTests
    {
        private Mock<ICardService> _cardServiceMock = null!;
        private Mock<ICollectionRepository> _collectionRepositoryMock = null!;
        private Mock<IRazorPartialRenderer> _razorPartialRendererMock = null!;

        [TestMethod]
        public async Task OnGetAsync_PopulatesOrdersFromCardService()
        {
            _cardServiceMock.Setup(s => s.GetEnrichedOrdersAsync())
                .ReturnsAsync((IEnumerable<EditionAuditEntryViewModel>)[new EditionAuditEntryViewModel { CardName = "Dark Magician" }]);
            var page = CreatePage();

            await page.OnGetAsync();

            Assert.AreEqual(1, page.Orders.Count);
        }

        [TestMethod]
        public async Task OnPostDeleteAsync_DeletesEntryAndRedirects()
        {
            var page = CreatePage();

            var result = await page.OnPostDeleteAsync(5);

            _collectionRepositoryMock.Verify(r => r.DeleteAsync(5), Times.Once);
            Assert.IsInstanceOfType<RedirectToPageResult>(result);
        }

        [TestMethod]
        public async Task OnPostEditAsync_AjaxRequestNoMatch_ReturnsEmptyContent()
        {
            _cardServiceMock.Setup(s => s.GetEnrichedOrdersAsync())
                .ReturnsAsync((IEnumerable<EditionAuditEntryViewModel>)[]);
            var page = CreatePage(isAjax: true);

            var result = await page.OnPostEditAsync(999, 2, null, null, null, null, null, null) as ContentResult;

            Assert.AreEqual(string.Empty, result!.Content);
        }

        [TestMethod]
        public async Task OnPostEditAsync_AjaxRequestWithMatch_ReturnsRenderedPartial()
        {
            _cardServiceMock.Setup(s => s.GetEnrichedOrdersAsync())
                .ReturnsAsync((IEnumerable<EditionAuditEntryViewModel>)[new EditionAuditEntryViewModel { EntryID = 1, CardName = "Dark Magician" }]);
            _razorPartialRendererMock
                .Setup(r => r.RenderPartialAsync(It.IsAny<PageModel>(), "_OrderRow", It.IsAny<EditionAuditEntryViewModel>()))
                .ReturnsAsync("<tr>rendered</tr>");
            var page = CreatePage(isAjax: true);

            var result = await page.OnPostEditAsync(1, 2, null, null, null, null, null, null) as ContentResult;

            Assert.AreEqual("<tr>rendered</tr>", result!.Content);
        }

        [TestMethod]
        public async Task OnPostEditAsync_NonAjaxRequest_RedirectsToPage()
        {
            var page = CreatePage(isAjax: false);

            var result = await page.OnPostEditAsync(1, 2, null, null, null, null, null, null);

            _collectionRepositoryMock.Verify(r => r.UpdateAsync(It.Is<CollectionEntry>(e => e.ID == 1 && e.Quantity == 2)), Times.Once);
            Assert.IsInstanceOfType<RedirectToPageResult>(result);
        }

        [TestMethod]
        public async Task OnPostEditAsync_QuantityBelowOne_ClampsToOne()
        {
            CollectionEntry? captured = null;
            _collectionRepositoryMock
                .Setup(r => r.UpdateAsync(It.IsAny<CollectionEntry>()))
                .Callback<CollectionEntry>(e => captured = e)
                .ReturnsAsync(true);
            var page = CreatePage();

            await page.OnPostEditAsync(1, 0, null, null, null, null, null, null);

            Assert.AreEqual(1, captured!.Quantity);
        }

        [TestMethod]
        public async Task OnPostEditAsync_RarityNameProvided_StoresRarityName()
        {
            CollectionEntry? captured = null;
            _collectionRepositoryMock
                .Setup(r => r.UpdateAsync(It.IsAny<CollectionEntry>()))
                .Callback<CollectionEntry>(e => captured = e)
                .ReturnsAsync(true);
            var page = CreatePage();

            await page.OnPostEditAsync(1, 2, null, null, null, null, null, null, rarityName: "Ultra Rare");

            Assert.AreEqual("Ultra Rare", captured!.RarityName);
        }

        [TestMethod]
        public async Task OnPostMarkOwnedAsync_QuantityBelowOne_ClampsToOne()
        {
            var page = CreatePage();

            await page.OnPostMarkOwnedAsync(1, 0);

            _collectionRepositoryMock.Verify(r => r.UpdateStatusAsync(1, CollectionStatus.Owned, 1), Times.Once);
        }

        [TestMethod]
        public async Task OnPostMarkOwnedAsync_RedirectsToPage()
        {
            var page = CreatePage();

            var result = await page.OnPostMarkOwnedAsync(1, 2);

            Assert.IsInstanceOfType<RedirectToPageResult>(result);
        }

        [TestInitialize]
        public void Setup()
        {
            _cardServiceMock = new Mock<ICardService>();
            _collectionRepositoryMock = new Mock<ICollectionRepository>();
            _razorPartialRendererMock = new Mock<IRazorPartialRenderer>();
        }

        private OrdersModel CreatePage(bool isAjax = false)
        {
            var page = new OrdersModel(_cardServiceMock.Object, _collectionRepositoryMock.Object, _razorPartialRendererMock.Object);
            PageContextFactory.Attach(page, httpContext =>
            {
                if (isAjax)
                    httpContext.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
            });
            return page;
        }
    }
}
