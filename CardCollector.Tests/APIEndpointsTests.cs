using CardCollector.Data.Models;
using CardCollector.Services;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using System.Reflection;

namespace CardCollector.Tests
{
    [TestClass]
    public sealed class APIEndpointsTests
    {
        [TestMethod]
        [DataRow(0, DisplayName = "Zero card ID")]
        [DataRow(-1, DisplayName = "Negative card ID")]
        public async Task GetCardPriceHistoryAsync_NonPositiveCardID_ReturnsBadRequest(int cardID)
        {
            var cardServiceMock = new Mock<ICardService>();

            var result = await APIEndpoints.GetCardPriceHistoryAsync(cardID, cardServiceMock.Object);

            Assert.IsInstanceOfType<BadRequest<string>>(result);
        }

        [TestMethod]
        public async Task GetCardPriceHistoryAsync_ValidCardID_ReturnsJsonSeries()
        {
            var cardServiceMock = new Mock<ICardService>();
            cardServiceMock.Setup(s => s.GetCardPriceHistoryAsync(1)).ReturnsAsync(
            [
                new CardPriceHistorySeries { Label = "LOB — Ultra Rare", Dates = ["2026-01-01"], Values = [10m] }
            ]);

            var result = await APIEndpoints.GetCardPriceHistoryAsync(1, cardServiceMock.Object);

            var json = result as IValueHttpResult;
            Assert.IsNotNull(json);
            var series = ((System.Collections.IEnumerable)json.Value!).Cast<object>().Single();
            Assert.AreEqual("LOB — Ultra Rare", GetPropertyValue(series, "label"));
        }

        [TestMethod]
        public async Task GetPriceAsync_UnparsableEditionString_TreatsEditionAsNull()
        {
            var pricingServiceMock = new Mock<IPricingService>();
            pricingServiceMock.Setup(p => p.GetPrintingPriceAsync(4210, null)).ReturnsAsync(5.00m);

            var result = await APIEndpoints.GetPriceAsync(4210, "not-a-real-edition", pricingServiceMock.Object);

            var json = result as IValueHttpResult;
            Assert.AreEqual(5.00m, GetPropertyValue(json!.Value, "price"));
        }

        [TestMethod]
        public async Task GetPriceAsync_ValidEditionString_ReturnsPrintingPriceForThatEdition()
        {
            var pricingServiceMock = new Mock<IPricingService>();
            pricingServiceMock.Setup(p => p.GetPrintingPriceAsync(4210, CardEdition.Unlimited)).ReturnsAsync(3.25m);

            var result = await APIEndpoints.GetPriceAsync(4210, "Unlimited", pricingServiceMock.Object);

            var json = result as IValueHttpResult;
            Assert.AreEqual(3.25m, GetPropertyValue(json!.Value, "price"));
        }

        private static object? GetPropertyValue(object? source, string propertyName) =>
                                                            source?.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)?.GetValue(source);
    }
}
