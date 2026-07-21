using CardCollector.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CardCollector.Tests.ViewModels
{
    [TestClass]
    public sealed class CardPrintingTests
    {
        [TestMethod]
        public void WithPrice_NullPrice_SetsPriceToNull()
        {
            var original = new CardPrinting { Price = 10.00m };

            var result = original.WithPrice(null);

            Assert.IsNull(result.Price);
        }

        [TestMethod]
        public void WithPrice_ReturnsCopyWithUpdatedPriceAndSameOtherFields()
        {
            var original = new CardPrinting
            {
                CardID = 1,
                CardName = "Blue-Eyes White Dragon",
                CardType = "Normal Monster",
                ImageID = 2,
                ImageURLSmall = "https://example.com/small.jpg",
                Price = 10.00m,
                RarityCode = "(UR)",
                RarityName = "Ultra Rare",
                SetCode = "LOB-EN001",
                SetName = "Legend of Blue Eyes White Dragon",
                AvailableRarities = ["Ultra Rare", "Common"]
            };

            var result = original.WithPrice(25.50m);

            Assert.AreEqual(25.50m, result.Price);
            Assert.AreEqual(original.CardID, result.CardID);
            Assert.AreEqual(original.CardName, result.CardName);
            Assert.AreEqual(original.SetCode, result.SetCode);
            Assert.AreSame(original.AvailableRarities, result.AvailableRarities);
        }
    }
}
