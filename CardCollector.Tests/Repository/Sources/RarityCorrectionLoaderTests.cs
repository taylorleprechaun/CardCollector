using CardCollector.Repository.Sources;

namespace CardCollector.Tests.Repository.Sources
{
    [TestClass]
    public sealed class RarityCorrectionLoaderTests
    {
        [TestMethod]
        public void Parse_DuplicateEntries_KeepsFirst()
        {
            const string json = """
                [
                  { "SetCode": "SYN-EN001", "OldRarityName": "Bad", "NewRarityName": "Ultra Rare" },
                  { "SetCode": "syn-en001", "OldRarityName": "bad", "NewRarityName": "Secret Rare" }
                ]
                """;

            var table = RarityCorrectionLoader.Parse(json);

            Assert.AreEqual("Ultra Rare", table[("SYN-EN001", "BAD")]);
        }

        [TestMethod]
        public void Parse_EntryMissingField_IsSkipped()
        {
            const string json = """[ { "SetCode": "SYN-EN001", "OldRarityName": "Bad" } ]""";

            var table = RarityCorrectionLoader.Parse(json);

            Assert.AreEqual(0, table.Count);
        }

        [TestMethod]
        public void Parse_MalformedJson_ReturnsEmptyTable()
        {
            var table = RarityCorrectionLoader.Parse("{ not json");

            Assert.AreEqual(0, table.Count);
        }
    }
}
