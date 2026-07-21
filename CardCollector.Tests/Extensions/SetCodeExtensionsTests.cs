using CardCollector.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CardCollector.Tests.Extensions
{
    [TestClass]
    public sealed class SetCodeExtensionsTests
    {
        [TestMethod]
        public void ToTCGPlayerSetCode_EmptyString_ReturnsEmptyString()
        {
            var result = string.Empty.ToTCGPlayerSetCode();

            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        [DataRow("LOB-EN124", "LOB", DisplayName = "Standard set code")]
        [DataRow("RA01-EN038", "RA01", DisplayName = "Alphanumeric set prefix")]
        public void ToTCGPlayerSetCode_SetCodeHasHyphen_ReturnsPrefix(string setCode, string expected)
        {
            var result = setCode.ToTCGPlayerSetCode();

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void ToTCGPlayerSetCode_SetCodeHasNoHyphen_ReturnsWholeString()
        {
            var result = "LOB".ToTCGPlayerSetCode();

            Assert.AreEqual("LOB", result);
        }
    }
}
