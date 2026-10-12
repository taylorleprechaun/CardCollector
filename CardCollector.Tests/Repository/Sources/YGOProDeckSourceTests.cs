using CardCollector.DTO.YGOProDeck;
using CardCollector.Repository.Sources;

namespace CardCollector.Tests.Repository.Sources
{
    [TestClass]
    public sealed class YGOProDeckSourceTests
    {
        [TestMethod]
        public void HasMiscInfo_CardHasMiscInfo_ReturnsTrue()
        {
            var cards = new List<YGOProDeckCard> { new() { ID = 1 }, new() { ID = 2, MiscInfo = [new YGOProDeckMiscInfo()] } };

            var result = YGOProDeckSource.HasMiscInfo(cards);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void HasMiscInfo_NoCardHasMiscInfo_ReturnsFalse()
        {
            var cards = new List<YGOProDeckCard> { new() { ID = 1 }, new() { ID = 2 } };

            var result = YGOProDeckSource.HasMiscInfo(cards);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void ParseCards_MalformedJson_ReturnsEmpty()
        {
            var result = YGOProDeckSource.ParseCards("{ not json");

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void ParseCards_MiscInfoAbsent_LeavesKonamiAndBetaIDNull()
        {
            const string json = """{"data":[{"id":12345678,"name":"Synthetic Card"}]}""";

            var card = YGOProDeckSource.ParseCards(json).Single();

            Assert.IsNull(card.KonamiID);
            Assert.IsNull(card.BetaID);
        }

        [TestMethod]
        public void ParseCards_MiscInfoPresent_CopiesKonamiIDAndExposesBetaID()
        {
            const string json = """
                {"data":[{"id":52245126,"name":"Synthetic Frost Beast","card_images":[{"id":52245126}],
                  "misc_info":[{"views":1,"beta_id":101402101,"konami_id":23513}]}]}
                """;

            var card = YGOProDeckSource.ParseCards(json).Single();

            Assert.AreEqual(52245126, card.ID);
            Assert.AreEqual(23513, card.KonamiID);
            Assert.AreEqual(101402101, card.BetaID);
            Assert.AreEqual(52245126, card.CardImages![0].ID);
        }

        [TestMethod]
        public void ParseCards_NoDataProperty_ReturnsEmpty()
        {
            var result = YGOProDeckSource.ParseCards("{}");

            Assert.AreEqual(0, result.Count);
        }
    }
}
