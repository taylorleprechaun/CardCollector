using CardCollector.Rules.Catalog;

namespace CardCollector.Tests.Rules.Catalog
{
    [TestClass]
    public sealed class TCGPlayerLinksTests
    {
        [TestMethod]
        [DataRow(null, DisplayName = "Null key")]
        [DataRow("", DisplayName = "Empty key")]
        [DataRow("tcg:abc", DisplayName = "Non-numeric key")]
        [DataRow("-5", DisplayName = "Negative key")]
        public void ProductURL_KeyThatIsNotAProductID_ReturnsNull(string? externalKey)
        {
            var result = TCGPlayerLinks.ProductURL(externalKey);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void ProductURL_NumericExternalKey_ReturnsTheProductPage()
        {
            var result = TCGPlayerLinks.ProductURL("69577");

            Assert.AreEqual("https://www.tcgplayer.com/product/69577", result);
        }

        [TestMethod]
        public void ProductURL_ProductID_ReturnsTheProductPage()
        {
            var result = TCGPlayerLinks.ProductURL(108329);

            Assert.AreEqual("https://www.tcgplayer.com/product/108329", result);
        }
    }
}
