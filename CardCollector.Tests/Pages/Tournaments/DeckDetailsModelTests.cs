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
        public async Task OnGetAsync_DeckExists_PassesViewEventIDAndListDateToLegalityService()
        {
            var detail = BuildDetail();
            var (model, _, legalityService) = CreateModel(4, detail);
            model.View = DeckLegalityView.Current;
            model.EventID = 7;
            model.ListDate = new DateOnly(2024, 4, 15);

            await model.OnGetAsync(CancellationToken.None);

            legalityService.Verify(s => s.GetAsync(
                detail, DeckLegalityView.Current, 7, new DateOnly(2024, 4, 15), It.IsAny<CancellationToken>()));
        }

        [TestMethod]
        public async Task OnGetAsync_DeckExists_ReturnsPageWithDetail()
        {
            var detail = BuildDetail();
            var (model, _, _) = CreateModel(4, detail);

            var result = await model.OnGetAsync(CancellationToken.None);

            Assert.IsInstanceOfType<PageResult>(result);
            Assert.AreSame(detail, model.Detail);
        }

        [TestMethod]
        public async Task OnGetAsync_DeckExists_SetsLegalityFromService()
        {
            var legality = BuildLegality();
            var (model, _, _) = CreateModel(4, BuildDetail(), legality);

            await model.OnGetAsync(CancellationToken.None);

            Assert.AreSame(legality, model.Legality);
        }
        [TestMethod]
        public async Task OnGetAsync_DeckMissing_DoesNotCallLegalityService()
        {
            var (model, _, legalityService) = CreateModel(99, null);

            await model.OnGetAsync(CancellationToken.None);

            legalityService.Verify(
                s => s.GetAsync(It.IsAny<DeckDetailViewModel>(), It.IsAny<DeckLegalityView?>(), It.IsAny<int?>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [TestMethod]
        public async Task OnGetAsync_DeckMissing_ReturnsNotFound()
        {
            var (model, _, _) = CreateModel(99, null);

            var result = await model.OnGetAsync(CancellationToken.None);

            Assert.IsInstanceOfType<NotFoundResult>(result);
            Assert.IsNull(model.Detail);
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

        private static DeckLegalityViewModel BuildLegality() =>
            new()
            {
                ActiveView = DeckLegalityView.Current,
                AvailableListDates = [],
                DeckID = 4,
                Events = [],
                IsAvailable = false
            };

        private static (DetailsModel Model, Mock<IDeckService> Decks, Mock<IDeckLegalityService> Legality) CreateModel(
            int id, DeckDetailViewModel? detail, DeckLegalityViewModel? legality = null)
        {
            var decks = new Mock<IDeckService>();
            decks.Setup(s => s.GetAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(detail);

            var legalityService = new Mock<IDeckLegalityService>();
            legalityService
                .Setup(s => s.GetAsync(It.IsAny<DeckDetailViewModel>(), It.IsAny<DeckLegalityView?>(), It.IsAny<int?>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(legality ?? BuildLegality());

            var model = new DetailsModel(legalityService.Object, decks.Object) { ID = id };
            PageContextFactory.Attach(model);
            return (model, decks, legalityService);
        }
    }
}
