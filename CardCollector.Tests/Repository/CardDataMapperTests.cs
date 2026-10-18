using CardCollector.DTO;
using CardCollector.DTO.YamlYugi;
using CardCollector.Repository;

namespace CardCollector.Tests.Repository
{
    [TestClass]
    public sealed class CardDataMapperTests
    {
        [TestMethod]
        public void BuildFallbackImage_ReturnsExpectedUrlPattern()
        {
            var image = CardDataMapper.BuildFallbackImage(7);

            Assert.AreEqual(7, image.ID);
            Assert.AreEqual("https://images.ygoprodeck.com/images/cards/7.jpg", image.ImageURL);
            Assert.AreEqual("https://images.ygoprodeck.com/images/cards_small/7.jpg", image.ImageURLSmall);
        }

        [TestMethod]
        public void BuildSetNameIndex_MultiplePrefixNamesOfDifferingLength_PicksShortestAsCanonical()
        {
            var cards = new List<Card>
            {
                new() { CardSets = [new Set { Code = "LOB-EN001", Name = "Legend of Blue Eyes White Dragon (Reprint)" }] },
                new() { CardSets = [new Set { Code = "LOB-EN002", Name = "Legend of Blue Eyes White Dragon" }] }
            };

            var (namesByCode, prefixByName) = CardDataMapper.BuildSetNameIndex(cards);

            Assert.AreEqual("Legend of Blue Eyes White Dragon", namesByCode["LOB-EN001"]);
            Assert.AreEqual("Legend of Blue Eyes White Dragon", namesByCode["LOB-EN002"]);
            Assert.AreEqual("LOB", prefixByName["Legend of Blue Eyes White Dragon"]);
        }

        [TestMethod]
        public void BuildSetNameIndex_SetWithNullCodeOrName_IsSkipped()
        {
            var cards = new List<Card>
            {
                new() { CardSets = [new Set { Code = null, Name = "Some Set" }, new Set { Code = "ABC-EN001", Name = null }] }
            };

            var (namesByCode, _) = CardDataMapper.BuildSetNameIndex(cards);

            Assert.AreEqual(0, namesByCode.Count);
        }

        [TestMethod]
        public void DeriveCardType_Monster_DelegatesToDeriveMonsterType()
        {
            var result = CardDataMapper.DeriveCardType(new YamlCard { CardType = "Monster", MonsterTypeLine = "Dragon / Fusion / Effect" });

            Assert.AreEqual("Fusion Monster", result);
        }

        [TestMethod]
        [DataRow("Spell", "Spell Card")]
        [DataRow("Trap", "Trap Card")]
        public void DeriveCardType_SpellOrTrap_ReturnsCardTypeDirectly(string yamlCardType, string expected)
        {
            var result = CardDataMapper.DeriveCardType(new YamlCard { CardType = yamlCardType });

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        [DataRow(null, "Normal Monster", DisplayName = "Null type line")]
        [DataRow("Dragon / Fusion", "Fusion Monster", DisplayName = "Fusion")]
        [DataRow("Dragon / Synchro", "Synchro Monster", DisplayName = "Synchro")]
        [DataRow("Dragon / Xyz", "Xyz Monster", DisplayName = "Xyz")]
        [DataRow("Cyberse / Link", "Link Monster", DisplayName = "Link")]
        [DataRow("Spellcaster / Ritual", "Ritual Monster", DisplayName = "Ritual")]
        [DataRow("Spellcaster / Effect", "Effect Monster", DisplayName = "Effect")]
        [DataRow("Dragon / Normal", "Normal Monster", DisplayName = "No matching keyword")]
        public void DeriveMonsterType_ReturnsExpectedTypeForLine(string? typeLine, string expected)
        {
            var result = CardDataMapper.DeriveMonsterType(typeLine);

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void DeriveRace_MonsterTypeLinePresent_ReturnsFirstToken()
        {
            var result = CardDataMapper.DeriveRace(new YamlCard { MonsterTypeLine = "Spellcaster / Normal" });

            Assert.AreEqual("Spellcaster", result);
        }

        [TestMethod]
        public void DeriveRace_NeitherMonsterTypeLineNorProperty_ReturnsNull()
        {
            var result = CardDataMapper.DeriveRace(new YamlCard { MonsterTypeLine = null, Property = null });

            Assert.IsNull(result);
        }

        [TestMethod]
        public void DeriveRace_NoMonsterTypeLine_ReturnsProperty()
        {
            var result = CardDataMapper.DeriveRace(new YamlCard { MonsterTypeLine = null, Property = "Normal" });

            Assert.AreEqual("Normal", result);
        }

        [TestMethod]
        [DataRow("LOB-EN001", "LOB", DisplayName = "Standard code with hyphen")]
        [DataRow("LOB", "LOB", DisplayName = "No hyphen")]
        [DataRow("-EN001", "-EN001", DisplayName = "Hyphen at position zero is not treated as a separator")]
        public void GetSetPrefix_ReturnsExpectedPrefix(string code, string expected)
        {
            var result = CardDataMapper.GetSetPrefix(code);

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        [DataRow(null, false, DisplayName = "Null name")]
        [DataRow("Legend of Blue Eyes White Dragon", false, DisplayName = "Non-Speed-Duel set")]
        [DataRow("Speed Duel: Ultimate Predators", true, DisplayName = "Speed Duel set")]
        [DataRow("speed duel: streets of battle city", true, DisplayName = "Case-insensitive match")]
        public void IsSpeedDuelSet_ReturnsExpectedResult(string? setName, bool expected)
        {
            var result = CardDataMapper.IsSpeedDuelSet(setName);

            Assert.AreEqual(expected, result);
        }
    }
}
