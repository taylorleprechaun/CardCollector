using CardCollector.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CardCollector.Tests.Extensions
{
    [TestClass]
    public sealed class StringExtensionsTests
    {
        [TestMethod]
        [DataRow(null, DisplayName = "Null")]
        [DataRow("", DisplayName = "Empty")]
        [DataRow("   ", DisplayName = "Whitespace only")]
        public void TrimToNull_BlankText_ReturnsNull(string? value)
        {
            var result = value.TrimToNull();

            Assert.IsNull(result);
        }

        [TestMethod]
        public void TrimToNull_TextWithSurroundingSpaces_ReturnsTrimmedText()
        {
            var result = "  Top 8 \t".TrimToNull();

            Assert.AreEqual("Top 8", result);
        }
    }
}
