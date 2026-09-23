using System.Text.Json;
using CardCollector.Models;
using CardCollector.Pages.Tournaments;
using CardCollector.Services;
using CardCollector.Tests.TestHelpers;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CardCollector.Tests.Pages.Tournaments
{
    [TestClass]
    public sealed class DecksModelTests
    {
        [TestMethod]
        public async Task OnGetAsync_DecksExist_ExposesThem()
        {
            var (model, decks) = CreateModel();
            decks.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([new DeckListItemViewModel { EventCount = 2, ExtraCount = 15, ID = 3, MainCount = 60, Name = "Sample Deck", SideCount = 15 }]);

            await model.OnGetAsync(CancellationToken.None);

            Assert.AreEqual("Sample Deck", model.Decks.Single().Name);
        }

        [TestMethod]
        public async Task OnPostDeleteAsync_DeckExists_SetsMessageAndRedirects()
        {
            var (model, decks) = CreateModel();
            decks.Setup(s => s.DeleteAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(true);

            var result = await model.OnPostDeleteAsync(3, null, CancellationToken.None);

            Assert.IsInstanceOfType<RedirectToPageResult>(result);
            Assert.AreEqual("Deck deleted.", model.TempData["Success"]);
        }

        [TestMethod]
        public async Task OnPostDeleteAsync_DeckMissingFromAjax_ReturnsNotFound()
        {
            var (model, decks) = CreateModel(ajax: true);
            decks.Setup(s => s.DeleteAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(false);

            var result = await model.OnPostDeleteAsync(3, null, CancellationToken.None);

            Assert.IsInstanceOfType<NotFoundObjectResult>(result);
        }

        [TestMethod]
        public async Task OnPostImportAsync_AjaxFailure_ReturnsBadRequestWithErrors()
        {
            var (model, decks) = CreateModel(ajax: true);
            decks.Setup(s => s.ImportAsync(It.IsAny<DeckImportRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(DeckImportResult.Failure("The main deck is empty."));

            var result = await model.OnPostImportAsync(null, false, null, null, "#main", CancellationToken.None);

            var bad = Assert.IsInstanceOfType<BadRequestObjectResult>(result);
            StringAssert.Contains(JsonSerializer.Serialize(bad.Value), "The main deck is empty.");
        }

        [TestMethod]
        public async Task OnPostImportAsync_AjaxSuccess_ReturnsOkAndLeavesMessageForTheReload()
        {
            var (model, decks) = CreateModel(ajax: true);
            decks.Setup(s => s.ImportAsync(It.IsAny<DeckImportRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DeckImportResult { DeckID = 4, ExtraCount = 15, LinkedEventCount = 3, MainCount = 60, SideCount = 15, UnknownPasscodes = [1, 2] });

            var result = await model.OnPostImportAsync(8, true, "Sample Deck", null, "ydke://", CancellationToken.None);

            var json = Assert.IsInstanceOfType<JsonResult>(result);
            StringAssert.Contains(JsonSerializer.Serialize(json.Value), "true");
            var message = (string)model.TempData["Success"]!;
            StringAssert.Contains(message, "60 main");
            StringAssert.Contains(message, "2 cards couldn't be matched");
            StringAssert.Contains(message, "Linked to 3 events");
        }

        [TestMethod]
        public async Task OnPostImportAsync_FormValues_AreSentToTheService()
        {
            var (model, decks) = CreateModel(ajax: true);
            DeckImportRequest? sent = null;
            decks.Setup(s => s.ImportAsync(It.IsAny<DeckImportRequest>(), It.IsAny<CancellationToken>()))
                .Callback<DeckImportRequest, CancellationToken>((request, _) => sent = request)
                .ReturnsAsync(new DeckImportResult());

            await model.OnPostImportAsync(8, true, "Sample Deck", null, "pasted text", CancellationToken.None);

            Assert.AreEqual(8, sent!.EventID);
            Assert.IsTrue(sent.LinkOtherEventsWithSameUrl);
            Assert.AreEqual("Sample Deck", sent.Name);
            Assert.AreEqual("pasted text", sent.Text);
        }

        [TestMethod]
        public async Task OnPostImportAsync_LocalReturnUrl_RedirectsThere()
        {
            var (model, decks) = CreateModel();
            var url = new Mock<IUrlHelper>();
            url.Setup(u => u.IsLocalUrl("/Tournaments/Events?pageNumber=2")).Returns(true);
            model.Url = url.Object;
            decks.Setup(s => s.ImportAsync(It.IsAny<DeckImportRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DeckImportResult { MainCount = 40 });

            var result = await model.OnPostImportAsync(null, false, "Sample", "/Tournaments/Events?pageNumber=2", "#main", CancellationToken.None);

            var redirect = Assert.IsInstanceOfType<LocalRedirectResult>(result);
            Assert.AreEqual("/Tournaments/Events?pageNumber=2", redirect.Url);
        }

        [TestMethod]
        public async Task OnPostImportAsync_NonAjaxFailure_SetsErrorAndRedirects()
        {
            var (model, decks) = CreateModel();
            decks.Setup(s => s.ImportAsync(It.IsAny<DeckImportRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(DeckImportResult.Failure("Deck name is required."));

            var result = await model.OnPostImportAsync(null, false, null, "https://elsewhere.example.test/", "#main", CancellationToken.None);

            Assert.IsInstanceOfType<RedirectToPageResult>(result);
            Assert.AreEqual("Deck name is required.", model.TempData["Error"]);
        }

        [TestMethod]
        public async Task OnPostLinkAsync_NothingLinkedFromAjax_ReturnsNotFound()
        {
            var (model, decks) = CreateModel(ajax: true);
            decks.Setup(s => s.LinkEventAsync(8, 3, false, It.IsAny<CancellationToken>())).ReturnsAsync(0);

            var result = await model.OnPostLinkAsync(3, 8, false, null, CancellationToken.None);

            Assert.IsInstanceOfType<NotFoundObjectResult>(result);
        }

        [TestMethod]
        public async Task OnPostLinkAsync_SeveralEventsLinked_ReportsTheCount()
        {
            var (model, decks) = CreateModel();
            decks.Setup(s => s.LinkEventAsync(8, 3, true, It.IsAny<CancellationToken>())).ReturnsAsync(3);

            await model.OnPostLinkAsync(3, 8, true, null, CancellationToken.None);

            Assert.AreEqual("Deck linked to 3 events.", model.TempData["Success"]);
        }
        [TestMethod]
        public async Task OnPostLinkAsync_SingleEventLinked_ReportsIt()
        {
            var (model, decks) = CreateModel();
            decks.Setup(s => s.LinkEventAsync(8, 3, false, It.IsAny<CancellationToken>())).ReturnsAsync(1);

            await model.OnPostLinkAsync(3, 8, false, null, CancellationToken.None);

            Assert.AreEqual("Deck linked.", model.TempData["Success"]);
        }

        [TestMethod]
        public void OnPostParse_InvalidText_ReturnsBadRequestWithTheError()
        {
            var (model, decks) = CreateModel();
            decks.Setup(s => s.Preview("nonsense")).Returns(DeckImportResult.Failure("That is not a deck."));

            var result = model.OnPostParse("nonsense");

            var bad = Assert.IsInstanceOfType<BadRequestObjectResult>(result);
            StringAssert.Contains(JsonSerializer.Serialize(bad.Value), "That is not a deck.");
        }

        [TestMethod]
        public void OnPostParse_ValidText_ReturnsTheCounts()
        {
            var (model, decks) = CreateModel();
            decks.Setup(s => s.Preview("ydke://")).Returns(new DeckImportResult { ExtraCount = 15, MainCount = 60, SideCount = 15, UnknownPasscodes = [7] });

            var result = model.OnPostParse("ydke://");

            var json = Assert.IsInstanceOfType<JsonResult>(result);
            Assert.AreEqual("{\"extra\":15,\"main\":60,\"side\":15,\"unknown\":1}", JsonSerializer.Serialize(json.Value));
        }

        [TestMethod]
        public async Task OnPostRenameAsync_DeckMissing_ReturnsNotFoundForAjax()
        {
            var (model, decks) = CreateModel(ajax: true);
            decks.Setup(s => s.UpdateAsync(3, "Name", null, It.IsAny<CancellationToken>())).ReturnsAsync(SaveResult.Missing("Deck not found."));

            var result = await model.OnPostRenameAsync(3, "Name", null, null, CancellationToken.None);

            Assert.IsInstanceOfType<NotFoundObjectResult>(result);
        }

        [TestMethod]
        public async Task OnPostRenameAsync_InvalidName_SetsErrorAndRedirects()
        {
            var (model, decks) = CreateModel();
            decks.Setup(s => s.UpdateAsync(3, " ", null, It.IsAny<CancellationToken>())).ReturnsAsync(SaveResult.Failure(["Deck name is required."]));

            var result = await model.OnPostRenameAsync(3, " ", null, null, CancellationToken.None);

            Assert.IsInstanceOfType<RedirectToPageResult>(result);
            Assert.AreEqual("Deck name is required.", model.TempData["Error"]);
        }

        [TestMethod]
        public async Task OnPostRenameAsync_ValidValues_SetsMessage()
        {
            var (model, decks) = CreateModel();
            decks.Setup(s => s.UpdateAsync(3, "Name", "Note", It.IsAny<CancellationToken>())).ReturnsAsync(SaveResult.Success());

            await model.OnPostRenameAsync(3, "Name", "Note", null, CancellationToken.None);

            Assert.AreEqual("Deck updated.", model.TempData["Success"]);
        }

        [TestMethod]
        public async Task OnPostUnlinkAsync_EventExists_SetsMessage()
        {
            var (model, decks) = CreateModel();
            decks.Setup(s => s.UnlinkEventAsync(8, It.IsAny<CancellationToken>())).ReturnsAsync(true);

            await model.OnPostUnlinkAsync(8, null, CancellationToken.None);

            Assert.AreEqual("Deck removed from the event.", model.TempData["Success"]);
        }

        [TestMethod]
        public async Task OnPostUnlinkAsync_EventMissing_SetsErrorForNonAjax()
        {
            var (model, decks) = CreateModel();
            decks.Setup(s => s.UnlinkEventAsync(8, It.IsAny<CancellationToken>())).ReturnsAsync(false);

            await model.OnPostUnlinkAsync(8, null, CancellationToken.None);

            Assert.AreEqual("That event no longer exists.", model.TempData["Error"]);
        }

        private static (DecksModel Model, Mock<IDeckService> Decks) CreateModel(bool ajax = false)
        {
            var decks = new Mock<IDeckService>();
            var model = new DecksModel(decks.Object);
            PageContextFactory.Attach(model, http =>
            {
                if (ajax)
                    http.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
            });

            return (model, decks);
        }
    }
}
