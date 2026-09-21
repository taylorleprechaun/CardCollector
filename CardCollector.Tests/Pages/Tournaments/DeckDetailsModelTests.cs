using CardCollector.Data.Models;
using CardCollector.Pages.Tournaments.Decks;
using CardCollector.Services;
using CardCollector.Tests.TestHelpers;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Moq;

namespace CardCollector.Tests.Pages.Tournaments
{
    [TestClass]
    public sealed class DeckDetailsModelTests
    {
        [TestMethod]
        public async Task OnGetAsync_DeckExists_ReturnsPageWithDetail()
        {
            var detail = BuildDetail();
            var (model, _) = CreateModel(4, detail);

            var result = await model.OnGetAsync(CancellationToken.None);

            Assert.IsInstanceOfType<PageResult>(result);
            Assert.AreSame(detail, model.Detail);
        }

        [TestMethod]
        public async Task OnGetAsync_DeckMissing_ReturnsNotFound()
        {
            var (model, _) = CreateModel(99, null);

            var result = await model.OnGetAsync(CancellationToken.None);

            Assert.IsInstanceOfType<NotFoundResult>(result);
            Assert.IsNull(model.Detail);
        }

        [TestMethod]
        public void ReturnURL_DeckID_PointsBackAtThisDeck()
        {
            var (model, _) = CreateModel(4, null);

            Assert.AreEqual("/Tournaments/Decks/Details?id=4", model.ReturnURL);
        }

        private static DeckDetailViewModel BuildDetail()
        {
            var empty = new DeckSectionViewModel { Cards = [] };
            return new DeckDetailViewModel
            {
                Deck = new Deck { ID = 4, Name = "Sample Deck" },
                Events = [],
                Extra = empty,
                Main = empty,
                MainTypes = new DeckTypeCounts(0, 0, 0),
                Side = empty
            };
        }

        private static (DetailsModel Model, Mock<IDeckService> Decks) CreateModel(int id, DeckDetailViewModel? detail)
        {
            var decks = new Mock<IDeckService>();
            decks.Setup(s => s.GetAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(detail);

            var model = new DetailsModel(decks.Object) { ID = id };
            PageContextFactory.Attach(model);
            return (model, decks);
        }
    }
}
