using CardCollector.Models;
using CardCollector.Rules;
using Microsoft.Extensions.Logging;
using Moq;

namespace CardCollector.Tests.Rules
{
    [TestClass]
    public sealed class BanlistParserTests
    {
        [TestMethod]
        public void FilterDatedListNames_MixedNames_ReturnsOnlyDatedVectorJson()
        {
            var names = new[]
            {
                "current.vector.json",
                "options.json",
                "2024-04-22.vector.json",
                "2024-04-22.raw.json",
                "2026-05-18.vector.json",
                "not-a-date.vector.json"
            };

            var result = BanlistParser.FilterDatedListNames(names);

            CollectionAssert.AreEquivalent(new[] { "2024-04-22.vector.json", "2026-05-18.vector.json" }, result.ToArray());
        }

        [TestMethod]
        public void FilterDatedListNames_NullFileNames_ThrowsArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => BanlistParser.FilterDatedListNames(null!));
        }

        [TestMethod]
        public void ParseList_DateNotParseable_ReturnsNull()
        {
            var result = BanlistParser.ParseList("""{ "date": "not-a-date", "regulation": { "123": 0 } }""", Logger());

            Assert.IsNull(result);
        }

        [TestMethod]
        public void ParseList_EmptyOrWhitespaceJson_ReturnsNull()
        {
            var result = BanlistParser.ParseList("   ", Logger());

            Assert.IsNull(result);
        }

        [TestMethod]
        public void ParseList_JsonIsNullLiteral_ReturnsNull()
        {
            var result = BanlistParser.ParseList("null", Logger());

            Assert.IsNull(result);
        }

        [TestMethod]
        public void ParseList_MalformedJson_ReturnsNull()
        {
            var result = BanlistParser.ParseList("{ this is not valid json", Logger());

            Assert.IsNull(result);
        }

        [TestMethod]
        public void ParseList_MissingDate_ReturnsNull()
        {
            var result = BanlistParser.ParseList("""{ "regulation": { "123": 0 } }""", Logger());

            Assert.IsNull(result);
        }

        [TestMethod]
        public void ParseList_NonNumericKonamiID_SkipsThatEntryOnly()
        {
            var json = """{ "date": "2024-01-01", "regulation": { "abc": 1, "123": 0 } }""";

            var result = BanlistParser.ParseList(json, Logger());

            Assert.IsNotNull(result);
            Assert.AreEqual(1, result!.LimitsByKonamiID.Count);
            Assert.AreEqual(BanlistLimit.Forbidden, result.LimitsByKonamiID[123]);
        }

        [TestMethod]
        public void ParseList_NoRegulationField_ReturnsEmptyLimits()
        {
            var result = BanlistParser.ParseList("""{ "date": "2024-01-01" }""", Logger());

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result!.LimitsByKonamiID.Count);
        }
        [TestMethod]
        public void ParseList_OutOfRangeValue_SkipsThatEntry()
        {
            var json = """{ "date": "2024-01-01", "regulation": { "123": 5 } }""";

            var result = BanlistParser.ParseList(json, Logger());

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result!.LimitsByKonamiID.Count);
        }

        [TestMethod]
        public void ParseList_ValidJson_ReturnsBanlist()
        {
            var json = """{ "date": "2026-05-18", "regulation": { "12345": 0, "67890": 1, "11111": 2 } }""";

            var result = BanlistParser.ParseList(json, Logger());

            Assert.IsNotNull(result);
            Assert.AreEqual(new DateOnly(2026, 5, 18), result!.EffectiveDate);
            Assert.AreEqual(BanlistLimit.Forbidden, result.LimitsByKonamiID[12345]);
            Assert.AreEqual(BanlistLimit.Limited, result.LimitsByKonamiID[67890]);
            Assert.AreEqual(BanlistLimit.SemiLimited, result.LimitsByKonamiID[11111]);
        }

        private static ILogger Logger() => new Mock<ILogger>().Object;
    }
}
