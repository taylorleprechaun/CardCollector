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
        public async Task OnGetAsync_PopulatesStatsAndSerializesPriceHistoryCardsInCamelCase()
        {
            var cardServiceMock = new Mock<ICardService>();
            cardServiceMock.Setup(s => s.GetCollectionStatsAsync()).ReturnsAsync(new CollectionStatsViewModel());
            cardServiceMock.Setup(s => s.GetPriceHistoryCardsAsync()).ReturnsAsync(
            [
                new PriceHistoryCardViewModel { CardID = 7, ImageURL = "url.jpg", Name = "Dark Magician" }
            ]);
            var page = new StatsModel(cardServiceMock.Object);

            await page.OnGetAsync();

            Assert.AreEqual("[{\"cardID\":7,\"imageURL\":\"url.jpg\",\"name\":\"Dark Magician\"}]", page.PriceHistoryCardsJson);
        }
    }
}
