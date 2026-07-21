using CardCollector.Pages;
using CardCollector.Services;
using CardCollector.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CardCollector.Tests.Pages
{
    [TestClass]
    public sealed class IndexModelTests
    {
        [TestMethod]
        public async Task OnGetAsync_PopulatesStatsFromCardService()
        {
            var cardServiceMock = new Mock<ICardService>();
            cardServiceMock.Setup(s => s.GetDashboardStatsAsync()).ReturnsAsync(new DashboardStats { TotalCards = 42 });
            var page = new IndexModel(cardServiceMock.Object);

            await page.OnGetAsync();

            Assert.AreEqual(42, page.Stats.TotalCards);
        }
    }
}
