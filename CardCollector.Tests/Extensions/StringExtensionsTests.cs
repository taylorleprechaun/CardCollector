using CardCollector.Extensions;

namespace CardCollector.Tests.Extensions
{
    [TestClass]
    public sealed class StringExtensionsTests
    {
        [TestMethod]
        [DataRow("Kuriboh", "%Kuriboh%", DisplayName = "Plain text")]
        [DataRow("100%", @"%100\%%", DisplayName = "Percent")]
        [DataRow("SYN_EN", @"%SYN\_EN%", DisplayName = "Underscore")]
        [DataRow(@"a\b", @"%a\\b%", DisplayName = "Escape character")]
        public void ToContainsLikePattern_Text_WrapsAndEscapesIt(string value, string expected)
        {
            var result = value.ToContainsLikePattern();

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void ToStartsWithLikePattern_TextWithWildcard_EscapesItAndAppendsWildcard()
        {
            var result = "LOB_".ToStartsWithLikePattern();

            Assert.AreEqual(@"LOB\_%", result);
        }

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
