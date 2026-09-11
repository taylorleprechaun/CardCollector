using System.Reflection;
using CardCollector.Data.Models;
using CardCollector.Services;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CardCollector.Tests
{
    [TestClass]
    public sealed class APIEndpointsTests
    {
        [TestMethod]
        [DataRow(null, DisplayName = "Null card name")]
        [DataRow("", DisplayName = "Empty card name")]
        [DataRow("   ", DisplayName = "Whitespace card name")]
        public async Task GetCardPriceHistoryAsync_BlankCardName_ReturnsBadRequest(string? cardName)
        {
            var cardServiceMock = new Mock<ICardService>();

            var result = await APIEndpoints.GetCardPriceHistoryAsync(cardName!, cardServiceMock.Object);

            Assert.IsInstanceOfType<BadRequest<string>>(result);
        }

        [TestMethod]
        public async Task GetCardPriceHistoryAsync_ValidCardName_ReturnsJsonSeries()
        {
            var cardServiceMock = new Mock<ICardService>();
            cardServiceMock.Setup(s => s.GetCardPriceHistoryAsync("Dark Magician")).ReturnsAsync(
            [
                new CardPriceHistorySeries { Label = "LOB — Ultra Rare", Dates = ["2026-01-01"], Values = [10m] }
            ]);

            var result = await APIEndpoints.GetCardPriceHistoryAsync("Dark Magician", cardServiceMock.Object);

            var json = result as IValueHttpResult;
            Assert.IsNotNull(json);
            var series = ((System.Collections.IEnumerable)json.Value!).Cast<object>().Single();
            Assert.AreEqual("LOB — Ultra Rare", GetPropertyValue(series, "label"));
        }

        [TestMethod]
        public async Task GetPriceAsync_UnparsableEditionString_TreatsEditionAsNull()
        {
            var pricingServiceMock = new Mock<IPricingService>();
            pricingServiceMock
                .Setup(p => p.GetPrintingPriceAsync(1, "LOB-EN001", "Ultra Rare", null))
                .ReturnsAsync(5.00m);

            var result = await APIEndpoints.GetPriceAsync(1, "LOB-EN001", "Ultra Rare", "not-a-real-edition", null, pricingServiceMock.Object);

            var json = result as IValueHttpResult;
            Assert.IsNotNull(json);
            Assert.AreEqual(5.00m, GetPropertyValue(json.Value, "price"));
        }

        [TestMethod]
        public async Task GetPriceAsync_ValidEditionString_ParsesEditionAndReturnsPrice()
        {
            var pricingServiceMock = new Mock<IPricingService>();
            pricingServiceMock
                .Setup(p => p.GetPrintingPriceAsync(1, "LOB-EN001", "Ultra Rare", CardEdition.FirstEdition))
                .ReturnsAsync(9.99m);

            var result = await APIEndpoints.GetPriceAsync(1, "LOB-EN001", "Ultra Rare", "FirstEdition", null, pricingServiceMock.Object);

            var json = result as IValueHttpResult;
            Assert.IsNotNull(json);
            Assert.AreEqual(9.99m, GetPropertyValue(json.Value, "price"));
        }

        private static object? GetPropertyValue(object? source, string propertyName) =>
                                                            source?.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)?.GetValue(source);
    }
}
