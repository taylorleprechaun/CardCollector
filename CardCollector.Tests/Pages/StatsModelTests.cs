using CardCollector.Pages;
using CardCollector.Services;
using CardCollector.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CardCollector.Tests.Pages
{
    [TestClass]
    public sealed class StatsModelTests
    {
        [TestMethod]
        public async Task OnGetAsync_PopulatesStatsAndSerializesTrackedCardImageMap()
        {
            var cardServiceMock = new Mock<ICardService>();
            cardServiceMock.Setup(s => s.GetCollectionStatsAsync()).ReturnsAsync(new CollectionStatsViewModel());
            cardServiceMock.Setup(s => s.GetTrackedCardImageMapAsync())
                .ReturnsAsync(new Dictionary<string, string> { ["Dark Magician"] = "url.jpg" });
            var page = new StatsModel(cardServiceMock.Object);

            await page.OnGetAsync();

            StringAssert.Contains(page.TrackedCardImageMapJson, "Dark Magician");
            StringAssert.Contains(page.TrackedCardImageMapJson, "url.jpg");
        }
    }
}
