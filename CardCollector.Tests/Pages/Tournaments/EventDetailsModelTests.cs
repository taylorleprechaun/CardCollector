using CardCollector.Data.Models;
using CardCollector.Pages.Tournaments.Events;
using CardCollector.Services;
using CardCollector.Tests.TestHelpers;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Moq;

namespace CardCollector.Tests.Pages.Tournaments
{
    [TestClass]
    public sealed class EventDetailsModelTests
    {
        [TestMethod]
        public async Task OnGetAsync_EventExists_ReturnsPageWithDetail()
        {
            var detail = new EventDetailViewModel
            {
                DiceRecord = new DiceRecord(0, 0),
                Event = new Event { ID = 5, Location = "Test Hobby Shop" },
                GameRecord = new WinLossTie(0, 0, 0),
                MatchRecord = new WinLossTie(0, 0, 0)
            };
            var (model, _) = CreateModel(5, detail);

            var result = await model.OnGetAsync(CancellationToken.None);

            Assert.IsInstanceOfType<PageResult>(result);
            Assert.AreSame(detail, model.Detail);
        }

        [TestMethod]
        public async Task OnGetAsync_EventMissing_ReturnsNotFound()
        {
            var (model, _) = CreateModel(999, null);

            var result = await model.OnGetAsync(CancellationToken.None);

            Assert.IsInstanceOfType<NotFoundResult>(result);
            Assert.IsNull(model.Detail);
        }

        [TestMethod]
        public async Task OnGetAsync_NoIDSupplied_ReturnsNotFound()
        {
            var (model, service) = CreateModel(0, null);

            var result = await model.OnGetAsync(CancellationToken.None);

            Assert.IsInstanceOfType<NotFoundResult>(result);
            service.Verify(s => s.GetAsync(0, It.IsAny<CancellationToken>()), Times.Once);
        }

        private static (DetailsModel Model, Mock<IEventService> Service) CreateModel(int id, EventDetailViewModel? detail)
        {
            var service = new Mock<IEventService>();
            service.Setup(s => s.GetAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(detail);

            var model = new DetailsModel(service.Object) { ID = id };
            PageContextFactory.Attach(model);
            return (model, service);
        }
    }
}
