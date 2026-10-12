using CardCollector.Repository.Sources;

namespace CardCollector.Tests.Repository.Sources
{
    [TestClass]
    public sealed class YamlYugiSourceTests
    {
        [TestMethod]
        public void ParseYaml_CardWithoutPassword_KeepsDocumentWithFakePassword()
        {
            const string yaml = """
                ---
                konami_id: 10112
                password:
                name:
                  en: Synthetic God Card
                fake_password: 10000040
                """;

            var card = YamlYugiSource.ParseYaml(yaml).Single();

            Assert.AreEqual(10112, card.KonamiID);
            Assert.IsNull(card.Password);
            Assert.AreEqual(10000040, card.FakePassword);
        }

        [TestMethod]
        public void ParseYaml_MultipleDocuments_ParsesEach()
        {
            const string yaml = """
                ---
                konami_id: 1
                password: 11111111
                ---
                konami_id: 2
                password: 22222222
                sets:
                  en:
                  - set_number: SYN-EN001
                    set_name: Synthetic Set
                    rarities:
                    - Ultra Rare
                """;

            var cards = YamlYugiSource.ParseYaml(yaml);

            Assert.AreEqual(2, cards.Count);
            Assert.AreEqual(22222222, cards[1].Password);
            Assert.AreEqual("SYN-EN001", cards[1].Sets!.En![0].SetNumber);
            Assert.IsNull(cards[1].FakePassword);
        }

        [TestMethod]
        public void ParseYaml_UnparseableDocument_SkipsItAndKeepsTheRest()
        {
            const string yaml = """
                ---
                konami_id: not-a-number
                ---
                konami_id: 2
                password: 22222222
                """;

            var cards = YamlYugiSource.ParseYaml(yaml);

            Assert.AreEqual(1, cards.Count);
            Assert.AreEqual(2, cards[0].KonamiID);
        }
    }
}
