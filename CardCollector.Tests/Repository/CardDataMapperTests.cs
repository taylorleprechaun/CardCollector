using CardCollector.DTO;
using CardCollector.DTO.YamlYugi;
using CardCollector.Repository;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CardCollector.Tests.Repository
{
    [TestClass]
    public sealed class CardDataMapperTests
    {
        [TestMethod]
        public void AttachImages_CardHasMatchingImages_AttachesThem()
        {
            var cards = new List<Card> { new() { ID = 1 } };
            var images = new Dictionary<int, IReadOnlyList<Image>> { [1] = [new Image { ID = 100 }] };

            CardDataMapper.AttachImages(cards, images);

            Assert.AreEqual(1, cards[0].CardImages!.Count);
            Assert.AreEqual(100, cards[0].CardImages![0].ID);
        }

        [TestMethod]
        public void AttachImages_NoMatchingImages_AttachesFallbackImage()
        {
            var cards = new List<Card> { new() { ID = 42 } };

            CardDataMapper.AttachImages(cards, new Dictionary<int, IReadOnlyList<Image>>());

            Assert.AreEqual(1, cards[0].CardImages!.Count);
            Assert.AreEqual(42, cards[0].CardImages![0].ID);
            StringAssert.Contains(cards[0].CardImages![0].ImageURL, "42.jpg");
        }

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
        public void BuildSets_MultipleRarities_ExpandsToOneSetPerRarity()
        {
            var yamlCard = new YamlCard
            {
                Sets = new YamlCardSets
                {
                    En =
                    [
                        new YamlSetEntry { SetName = "Legend of Blue Eyes White Dragon", SetNumber = "LOB-EN001", Rarities = ["Common", "Ultra Rare"] }
                    ]
                }
            };

            var result = CardDataMapper.BuildSets(yamlCard);

            Assert.AreEqual(2, result.Count);
            CollectionAssert.AreEquivalent(new[] { "Common", "Ultra Rare" }, result.Select(s => s.RarityName).ToArray());
        }

        [TestMethod]
        public void BuildSets_NoSets_ReturnsEmptyList()
        {
            var result = CardDataMapper.BuildSets(new YamlCard { Sets = null });

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void BuildSets_SpeedDuelSet_IsExcluded()
        {
            var yamlCard = new YamlCard
            {
                Sets = new YamlCardSets
                {
                    En =
                    [
                        new YamlSetEntry { SetName = "Speed Duel: Ultimate Predators", SetNumber = "SBLS-EN001", Rarities = ["Common"] }
                    ]
                }
            };

            var result = CardDataMapper.BuildSets(yamlCard);

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void ConvertYamlCards_AtkDefAreNonNumeric_ParsesToNull()
        {
            var yamlCards = new List<YamlCard> { new() { Password = 1, Atk = "?", Def = "?" } };

            var result = CardDataMapper.ConvertYamlCards(yamlCards);

            Assert.IsNull(result[0].ATK);
            Assert.IsNull(result[0].DEF);
        }

        [TestMethod]
        public void ConvertYamlCards_LevelIsNullButRankIsSet_UsesRank()
        {
            var yamlCards = new List<YamlCard> { new() { Password = 1, Level = null, Rank = 4 } };

            var result = CardDataMapper.ConvertYamlCards(yamlCards);

            Assert.AreEqual(4, result[0].Level);
        }

        [TestMethod]
        public void ConvertYamlCards_NoLinkArrows_LinkRatingIsNull()
        {
            var yamlCards = new List<YamlCard> { new() { Password = 1, LinkArrows = null } };

            var result = CardDataMapper.ConvertYamlCards(yamlCards);

            Assert.IsNull(result[0].LinkRating);
        }

        [TestMethod]
        public void ConvertYamlCards_NoSets_CardSetsIsNull()
        {
            var yamlCards = new List<YamlCard> { new() { Password = 1, Sets = null } };

            var result = CardDataMapper.ConvertYamlCards(yamlCards);

            Assert.IsNull(result[0].CardSets);
        }

        [TestMethod]
        public void ConvertYamlCards_PasswordIsNull_CardIsSkipped()
        {
            var yamlCards = new List<YamlCard> { new() { Password = null, Name = new YamlLocalizedString { En = "No Password" } } };

            var result = CardDataMapper.ConvertYamlCards(yamlCards);

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void ConvertYamlCards_ValidCard_MapsAllFields()
        {
            var yamlCards = new List<YamlCard>
            {
                new()
                {
                    Password = 1,
                    Name = new YamlLocalizedString { En = "Dark Magician" },
                    Text = new YamlLocalizedString { En = "The ultimate wizard." },
                    Attribute = "DARK",
                    Level = 7,
                    Atk = "2500",
                    Def = "2100",
                    LinkArrows = ["Top", "Bottom"],
                    CardType = "Monster",
                    MonsterTypeLine = "Spellcaster / Normal"
                }
            };

            var result = CardDataMapper.ConvertYamlCards(yamlCards);

            Assert.AreEqual(1, result.Count);
            var card = result[0];
            Assert.AreEqual(1, card.ID);
            Assert.AreEqual("Dark Magician", card.Name);
            Assert.AreEqual("The ultimate wizard.", card.Description);
            Assert.AreEqual("DARK", card.Attribute);
            Assert.AreEqual(7, card.Level);
            Assert.AreEqual(2500, card.ATK);
            Assert.AreEqual(2100, card.DEF);
            Assert.AreEqual(2, card.LinkRating);
            Assert.AreEqual("Normal Monster", card.CardType);
            Assert.AreEqual("Spellcaster", card.Type);
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
    }
}
