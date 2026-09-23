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
        public void ApplyRarityCorrections_CardHasNullCardSets_IsSkipped()
        {
            var cards = new List<Card> { new() { ID = 1, CardSets = null } };
            var corrections = new Dictionary<(string, string), string> { [("RA01-EN001", "ULTIMATE RARE")] = "Prismatic Ultimate Rare" };

            var result = CardDataMapper.ApplyRarityCorrections(cards, corrections);

            Assert.AreEqual(0, result);
        }

        [TestMethod]
        public void ApplyRarityCorrections_MatchingRow_RewritesRarityNameAndCode()
        {
            var cards = new List<Card>
            {
                new() { ID = 1, CardSets = [new Set { Code = "RA01-EN001", RarityName = "Ultimate Rare", RarityCode = "(UtR)" }] }
            };
            var corrections = new Dictionary<(string, string), string> { [("RA01-EN001", "ULTIMATE RARE")] = "Prismatic Ultimate Rare" };

            var result = CardDataMapper.ApplyRarityCorrections(cards, corrections);

            Assert.AreEqual(1, result);
            Assert.AreEqual("Prismatic Ultimate Rare", cards[0].CardSets![0].RarityName);
            Assert.AreEqual("(PUR)", cards[0].CardSets![0].RarityCode);
        }

        [TestMethod]
        public void ApplyRarityCorrections_MatchIsCaseInsensitive_StillCorrects()
        {
            var cards = new List<Card>
            {
                new() { ID = 1, CardSets = [new Set { Code = "ra01-en001", RarityName = "ultimate rare" }] }
            };
            var corrections = new Dictionary<(string, string), string> { [("RA01-EN001", "ULTIMATE RARE")] = "Prismatic Ultimate Rare" };

            var result = CardDataMapper.ApplyRarityCorrections(cards, corrections);

            Assert.AreEqual(1, result);
            Assert.AreEqual("Prismatic Ultimate Rare", cards[0].CardSets![0].RarityName);
        }
        [TestMethod]
        public void ApplyRarityCorrections_MultipleCardsAndRows_ReturnsTotalCorrectedCount()
        {
            var cards = new List<Card>
            {
                new() { ID = 1, CardSets = [new Set { Code = "RA01-EN001", RarityName = "Ultimate Rare" }, new Set { Code = "RA01-EN001", RarityName = "Collector's Rare" }] },
                new() { ID = 2, CardSets = [new Set { Code = "RA02-EN001", RarityName = "Ultimate Rare" }] }
            };
            var corrections = new Dictionary<(string, string), string>
            {
                [("RA01-EN001", "ULTIMATE RARE")] = "Prismatic Ultimate Rare",
                [("RA01-EN001", "COLLECTOR'S RARE")] = "Prismatic Collector's Rare",
                [("RA02-EN001", "ULTIMATE RARE")] = "Prismatic Ultimate Rare"
            };

            var result = CardDataMapper.ApplyRarityCorrections(cards, corrections);

            Assert.AreEqual(3, result);
        }

        [TestMethod]
        public void ApplyRarityCorrections_NoMatchingEntry_RowIsUnchanged()
        {
            var cards = new List<Card>
            {
                new() { ID = 1, CardSets = [new Set { Code = "LOB-EN001", RarityName = "Ultra Rare", RarityCode = "(UR)" }] }
            };
            var corrections = new Dictionary<(string, string), string> { [("RA01-EN001", "ULTIMATE RARE")] = "Prismatic Ultimate Rare" };

            var result = CardDataMapper.ApplyRarityCorrections(cards, corrections);

            Assert.AreEqual(0, result);
            Assert.AreEqual("Ultra Rare", cards[0].CardSets![0].RarityName);
            Assert.AreEqual("(UR)", cards[0].CardSets![0].RarityCode);
        }

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
        public void ConvertYamlCards_KonamiIDMissing_IsNull()
        {
            var yamlCards = new List<YamlCard> { new() { Password = 1, KonamiID = null } };

            var result = CardDataMapper.ConvertYamlCards(yamlCards);

            Assert.IsNull(result[0].KonamiID);
        }

        [TestMethod]
        public void ConvertYamlCards_KonamiIDPresent_CarriesToCard()
        {
            var yamlCards = new List<YamlCard> { new() { Password = 1, KonamiID = 21470 } };

            var result = CardDataMapper.ConvertYamlCards(yamlCards);

            Assert.AreEqual(21470, result[0].KonamiID);
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
        public void EnrichWithPrintVariants_CardHasNullCardSets_DoesNotThrow()
        {
            var cards = new List<Card> { new() { Name = "Some Card", CardSets = null } };
            var catalogPrintings = new List<TCGPriceSet>
            {
                new() { CardName = "Some Card", Code = "ABC-EN001", RarityName = "Ultra Rare", PrintVariant = "Extended Art" }
            };

            CardDataMapper.EnrichWithPrintVariants(cards, catalogPrintings);

            Assert.IsNull(cards[0].CardSets);
        }

        [TestMethod]
        public void EnrichWithPrintVariants_CardNameMatchIsCaseInsensitive_AddsVariant()
        {
            var cards = new List<Card>
            {
                new() { Name = "dark magical curtain", CardSets = [new Set { Code = "mamo-en003", RarityName = "Ultra Rare" }] }
            };
            var catalogPrintings = new List<TCGPriceSet>
            {
                new() { CardName = "Dark Magical Curtain", Code = "MAMO-EN003", RarityName = "Ultra Rare", PrintVariant = null },
                new() { CardName = "Dark Magical Curtain", Code = "MAMO-EN003", RarityName = "Ultra Rare", PrintVariant = "Extended Art" }
            };

            CardDataMapper.EnrichWithPrintVariants(cards, catalogPrintings);

            Assert.AreEqual(2, cards[0].CardSets!.Count);
        }

        [TestMethod]
        public void EnrichWithPrintVariants_CatalogHasRarityNotOnAnyExistingSetRow_AddsNewSetRow()
        {
            var cards = new List<Card>
            {
                new() { Name = "PSY-Frame Driver", CardSets = [new Set { Code = "RA05-EN002", Name = "25th Anniversary Rarity Collection", RarityName = "Ultra Rare" }] }
            };
            var catalogPrintings = new List<TCGPriceSet>
            {
                new() { CardName = "PSY-Frame Driver", Code = "RA05-EN002", RarityName = "Prismatic Collector's Rare", PrintVariant = null }
            };

            var result = CardDataMapper.EnrichWithPrintVariants(cards, catalogPrintings);

            Assert.AreEqual(1, result);
            Assert.AreEqual(2, cards[0].CardSets!.Count);
            var newRow = cards[0].CardSets!.Single(s => s.RarityName == "Prismatic Collector's Rare");
            Assert.AreEqual("RA05-EN002", newRow.Code);
            Assert.AreEqual("(PCR)", newRow.RarityCode);
        }

        [TestMethod]
        public void EnrichWithPrintVariants_ExistingRaritySpelledDifferentlyFromCatalog_NewRowNotAdded()
        {
            var cards = new List<Card>
            {
                new() { Name = "Some Card", CardSets = [new Set { Code = "ABC-EN001", Name = "Some Set", RarityName = "Short Print" }] }
            };
            var catalogPrintings = new List<TCGPriceSet>
            {
                new() { CardName = "Some Card", Code = "ABC-EN001", RarityName = "Common", PrintVariant = null }
            };

            var result = CardDataMapper.EnrichWithPrintVariants(cards, catalogPrintings);

            Assert.AreEqual(0, result);
            Assert.AreEqual(1, cards[0].CardSets!.Count);
        }

        [TestMethod]
        public void EnrichWithPrintVariants_MultipleCardsHaveNewRarities_ReturnsTotalCount()
        {
            var cards = new List<Card>
            {
                new() { Name = "Card One", CardSets = [new Set { Code = "ABC-EN001", Name = "Set One", RarityName = "Ultra Rare" }] },
                new() { Name = "Card Two", CardSets = [new Set { Code = "DEF-EN002", Name = "Set Two", RarityName = "Ultra Rare" }] }
            };
            var catalogPrintings = new List<TCGPriceSet>
            {
                new() { CardName = "Card One", Code = "ABC-EN001", RarityName = "Secret Rare", PrintVariant = null },
                new() { CardName = "Card Two", Code = "DEF-EN002", RarityName = "Secret Rare", PrintVariant = null }
            };

            var result = CardDataMapper.EnrichWithPrintVariants(cards, catalogPrintings);

            Assert.AreEqual(2, result);
        }

        [TestMethod]
        public void EnrichWithPrintVariants_MultipleExistingRaritiesSameSetCode_NewRarityAddedOnce()
        {
            var cards = new List<Card>
            {
                new()
                {
                    Name = "Some Card",
                    CardSets =
                    [
                        new Set { Code = "ABC-EN001", Name = "Some Set", RarityName = "Common" },
                        new Set { Code = "ABC-EN001", Name = "Some Set", RarityName = "Ultra Rare" }
                    ]
                }
            };
            var catalogPrintings = new List<TCGPriceSet>
            {
                new() { CardName = "Some Card", Code = "ABC-EN001", RarityName = "Common", PrintVariant = null },
                new() { CardName = "Some Card", Code = "ABC-EN001", RarityName = "Ultra Rare", PrintVariant = null },
                new() { CardName = "Some Card", Code = "ABC-EN001", RarityName = "Secret Rare", PrintVariant = null }
            };

            var result = CardDataMapper.EnrichWithPrintVariants(cards, catalogPrintings);

            Assert.AreEqual(1, result);
            Assert.AreEqual(3, cards[0].CardSets!.Count);
        }

        [TestMethod]
        public void EnrichWithPrintVariants_MultipleVariantsForSameRarity_AddsOneEntryPerVariant()
        {
            var cards = new List<Card>
            {
                new() { Name = "Some Card", CardSets = [new Set { Code = "ABC-EN001", RarityName = "Ultra Rare" }] }
            };
            var catalogPrintings = new List<TCGPriceSet>
            {
                new() { CardName = "Some Card", Code = "ABC-EN001", RarityName = "Ultra Rare", PrintVariant = null },
                new() { CardName = "Some Card", Code = "ABC-EN001", RarityName = "Ultra Rare", PrintVariant = "Extended Art" },
                new() { CardName = "Some Card", Code = "ABC-EN001", RarityName = "Ultra Rare", PrintVariant = "Alternate Art" }
            };

            CardDataMapper.EnrichWithPrintVariants(cards, catalogPrintings);

            Assert.AreEqual(3, cards[0].CardSets!.Count);
            CollectionAssert.AreEquivalent(
                new[] { null, "Extended Art", "Alternate Art" },
                cards[0].CardSets!.Select(s => s.PrintVariant).ToArray());
        }

        [TestMethod]
        public void EnrichWithPrintVariants_NewRarityHasPrintVariant_CarriesItOver()
        {
            var cards = new List<Card>
            {
                new() { Name = "Some Card", CardSets = [new Set { Code = "ABC-EN001", Name = "Some Set", RarityName = "Ultra Rare" }] }
            };
            var catalogPrintings = new List<TCGPriceSet>
            {
                new() { CardName = "Some Card", Code = "ABC-EN001", RarityName = "Secret Rare", PrintVariant = "Extended Art" }
            };

            CardDataMapper.EnrichWithPrintVariants(cards, catalogPrintings);

            var newRow = cards[0].CardSets!.Single(s => s.RarityName == "Secret Rare");
            Assert.AreEqual("Extended Art", newRow.PrintVariant);
        }

        [TestMethod]
        public void EnrichWithPrintVariants_NewRarityRow_InheritsSetNameFromSiblingRow()
        {
            var cards = new List<Card>
            {
                new() { Name = "Some Card", CardSets = [new Set { Code = "ABC-EN001", Name = "Rarity Collection", RarityName = "Ultra Rare" }] }
            };
            var catalogPrintings = new List<TCGPriceSet>
            {
                new() { CardName = "Some Card", Code = "ABC-EN001", RarityName = "Secret Rare", PrintVariant = null }
            };

            CardDataMapper.EnrichWithPrintVariants(cards, catalogPrintings);

            var newRow = cards[0].CardSets!.Single(s => s.RarityName == "Secret Rare");
            Assert.AreEqual("Rarity Collection", newRow.Name);
        }

        [TestMethod]
        public void EnrichWithPrintVariants_NoMatchingCardName_LeavesCardSetsUnchanged()
        {
            var cards = new List<Card>
            {
                new() { Name = "Some Other Card", CardSets = [new Set { Code = "MAMO-EN003", RarityName = "Ultra Rare" }] }
            };
            var catalogPrintings = new List<TCGPriceSet>
            {
                new() { CardName = "Dark Magical Curtain", Code = "MAMO-EN003", RarityName = "Ultra Rare", PrintVariant = "Extended Art" }
            };

            CardDataMapper.EnrichWithPrintVariants(cards, catalogPrintings);

            Assert.AreEqual(1, cards[0].CardSets!.Count);
        }

        [TestMethod]
        public void EnrichWithPrintVariants_NoNewRarityInCatalog_ReturnsZero()
        {
            var cards = new List<Card>
            {
                new() { Name = "Some Card", CardSets = [new Set { Code = "ABC-EN001", Name = "Some Set", RarityName = "Ultra Rare" }] }
            };
            var catalogPrintings = new List<TCGPriceSet>
            {
                new() { CardName = "Some Card", Code = "ABC-EN001", RarityName = "Ultra Rare", PrintVariant = null }
            };

            var result = CardDataMapper.EnrichWithPrintVariants(cards, catalogPrintings);

            Assert.AreEqual(0, result);
        }

        [TestMethod]
        public void EnrichWithPrintVariants_NoPlainPrintAndMultipleVariants_RewritesFirstAndAddsRestAsNewEntries()
        {
            var cards = new List<Card>
            {
                new() { Name = "Some Card", CardSets = [new Set { Code = "ABC-EN001", RarityName = "Ultra Rare" }] }
            };
            var catalogPrintings = new List<TCGPriceSet>
            {
                new() { CardName = "Some Card", Code = "ABC-EN001", RarityName = "Ultra Rare", PrintVariant = "Extended Art" },
                new() { CardName = "Some Card", Code = "ABC-EN001", RarityName = "Ultra Rare", PrintVariant = "Alternate Art" }
            };

            CardDataMapper.EnrichWithPrintVariants(cards, catalogPrintings);

            Assert.AreEqual(2, cards[0].CardSets!.Count);
            CollectionAssert.AreEquivalent(
                new[] { "Extended Art", "Alternate Art" },
                cards[0].CardSets!.Select(s => s.PrintVariant).ToArray());
        }

        [TestMethod]
        public void EnrichWithPrintVariants_NoPlainPrintExistsInCatalog_RewritesBaseEntryInPlaceInsteadOfDuplicating()
        {
            var cards = new List<Card>
            {
                new() { Name = "Dark Magical Curtain", CardSets = [new Set { Code = "MAMO-EN003", Name = "Magnificent Monsters", RarityName = "Starlight Rare" }] }
            };
            var catalogPrintings = new List<TCGPriceSet>
            {
                new() { CardName = "Dark Magical Curtain", Code = "MAMO-EN003", RarityName = "Starlight Rare", PrintVariant = "Extended Art" }
            };

            CardDataMapper.EnrichWithPrintVariants(cards, catalogPrintings);

            Assert.AreEqual(1, cards[0].CardSets!.Count);
            Assert.AreEqual("Extended Art", cards[0].CardSets![0].PrintVariant);
            Assert.AreEqual("MAMO-EN003", cards[0].CardSets![0].Code);
            Assert.AreEqual("Starlight Rare", cards[0].CardSets![0].RarityName);
        }

        [TestMethod]
        public void EnrichWithPrintVariants_PlainPrintAlsoExistsInCatalog_AddsNewSetEntryAlongsideBasePrint()
        {
            var cards = new List<Card>
            {
                new() { Name = "Dark Magical Curtain", CardSets = [new Set { Code = "MAMO-EN003", Name = "Magnificent Monsters", RarityName = "Ultra Rare" }] }
            };
            var catalogPrintings = new List<TCGPriceSet>
            {
                new() { CardName = "Dark Magical Curtain", Code = "MAMO-EN003", RarityName = "Ultra Rare", PrintVariant = null },
                new() { CardName = "Dark Magical Curtain", Code = "MAMO-EN003", RarityName = "Ultra Rare", PrintVariant = "Extended Art" }
            };

            CardDataMapper.EnrichWithPrintVariants(cards, catalogPrintings);

            Assert.AreEqual(2, cards[0].CardSets!.Count);
            Assert.IsNull(cards[0].CardSets![0].PrintVariant);
            Assert.AreEqual("Extended Art", cards[0].CardSets![1].PrintVariant);
            Assert.AreEqual("MAMO-EN003", cards[0].CardSets![1].Code);
            Assert.AreEqual("Ultra Rare", cards[0].CardSets![1].RarityName);
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
        public void HasKonamiIDs_AtLeastOneCardHasKonamiID_ReturnsTrue()
        {
            var cards = new List<Card> { new() { ID = 1, KonamiID = null }, new() { ID = 2, KonamiID = 100 } };

            var result = CardDataMapper.HasKonamiIDs(cards);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void HasKonamiIDs_EmptyList_ReturnsFalse()
        {
            var result = CardDataMapper.HasKonamiIDs([]);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void HasKonamiIDs_NoCardHasKonamiID_ReturnsFalse()
        {
            var cards = new List<Card> { new() { ID = 1, KonamiID = null }, new() { ID = 2, KonamiID = null } };

            var result = CardDataMapper.HasKonamiIDs(cards);

            Assert.IsFalse(result);
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

        [TestMethod]
        public void MergeMissingCards_AddedCardHasNullCardSets_DoesNotThrow()
        {
            var supplementalCards = new List<Card> { new() { ID = 2, CardSets = null } };

            var result = CardDataMapper.MergeMissingCards([], supplementalCards);

            Assert.IsNull(result[0].CardSets);
        }

        [TestMethod]
        public void MergeMissingCards_AddedCardHasSpeedDuelSet_StripsIt()
        {
            var supplementalCards = new List<Card>
            {
                new()
                {
                    ID = 2,
                    CardSets =
                    [
                        new Set { Code = "SBLS-EN001", Name = "Speed Duel: Ultimate Predators" },
                        new Set { Code = "LOB-EN001", Name = "Legend of Blue Eyes White Dragon" }
                    ]
                }
            };

            var result = CardDataMapper.MergeMissingCards([], supplementalCards);

            Assert.AreEqual(1, result[0].CardSets!.Count);
            Assert.AreEqual("LOB-EN001", result[0].CardSets![0].Code);
        }

        [TestMethod]
        public void MergeMissingCards_SupplementalCardAbsentFromPrimary_IsAdded()
        {
            var primaryCards = new List<Card> { new() { ID = 1, Name = "Dark Magician" } };
            var supplementalCards = new List<Card> { new() { ID = 2, Name = "Obelisk the Tormentor" } };

            var result = CardDataMapper.MergeMissingCards(primaryCards, supplementalCards);

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("Obelisk the Tormentor", result[1].Name);
        }

        [TestMethod]
        public void MergeMissingCards_SupplementalCardAlreadyInPrimary_IsNotDuplicated()
        {
            var primaryCards = new List<Card> { new() { ID = 1, Name = "Dark Magician (yaml-yugi)" } };
            var supplementalCards = new List<Card> { new() { ID = 1, Name = "Dark Magician (YGOProDeck)" } };

            var result = CardDataMapper.MergeMissingCards(primaryCards, supplementalCards);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("Dark Magician (yaml-yugi)", result[0].Name);
        }

        [TestMethod]
        public void MergeMissingSetPrintings_CardAbsentFromSupplemental_IsUnchanged()
        {
            var primaryCards = new List<Card> { new() { ID = 1, Name = "Dark Magician", CardSets = [new Set { Code = "LOB-EN005", RarityName = "Ultra Rare" }] } };
            var supplementalCards = new List<Card> { new() { ID = 2, Name = "Obelisk the Tormentor", CardSets = [new Set { Code = "LOB-EN005", RarityName = "Ultra Rare" }] } };

            var result = CardDataMapper.MergeMissingSetPrintings(primaryCards, supplementalCards);

            Assert.AreEqual(0, result);
            Assert.AreEqual(1, primaryCards[0].CardSets!.Count);
        }

        [TestMethod]
        public void MergeMissingSetPrintings_ComboAlreadyExists_IsNotDuplicated()
        {
            var primaryCards = new List<Card> { new() { ID = 1, Name = "Dark Magician", CardSets = [new Set { Code = "LOB-EN005", RarityName = "Ultra Rare" }] } };
            var supplementalCards = new List<Card> { new() { ID = 1, Name = "Dark Magician", CardSets = [new Set { Code = "LOB-EN005", RarityName = "Ultra Rare" }] } };

            var result = CardDataMapper.MergeMissingSetPrintings(primaryCards, supplementalCards);

            Assert.AreEqual(0, result);
            Assert.AreEqual(1, primaryCards[0].CardSets!.Count);
        }

        [TestMethod]
        public void MergeMissingSetPrintings_ComboMatchesViaNormalizedRaritySpelling_IsNotTreatedAsNew()
        {
            var primaryCards = new List<Card> { new() { ID = 1, Name = "Some Card", CardSets = [new Set { Code = "ABC-EN001", RarityName = "Short Print" }] } };
            var supplementalCards = new List<Card> { new() { ID = 1, Name = "Some Card", CardSets = [new Set { Code = "ABC-EN001", RarityName = "Common" }] } };

            var result = CardDataMapper.MergeMissingSetPrintings(primaryCards, supplementalCards);

            Assert.AreEqual(0, result);
            Assert.AreEqual(1, primaryCards[0].CardSets!.Count);
        }

        [TestMethod]
        public void MergeMissingSetPrintings_MultipleNewCombos_ReturnsTotalCount()
        {
            var primaryCards = new List<Card>
            {
                new() { ID = 1, Name = "Card One", CardSets = [new Set { Code = "ABC-EN001", RarityName = "Common" }] },
                new() { ID = 2, Name = "Card Two", CardSets = [new Set { Code = "DEF-EN002", RarityName = "Common" }] }
            };
            var supplementalCards = new List<Card>
            {
                new() { ID = 1, Name = "Card One", CardSets = [new Set { Code = "ABC-EN001", RarityName = "Common" }, new Set { Code = "ABC-EN001", RarityName = "Ultra Rare" }] },
                new() { ID = 2, Name = "Card Two", CardSets = [new Set { Code = "DEF-EN002", RarityName = "Common" }, new Set { Code = "DEF-EN002", RarityName = "Secret Rare" }] }
            };

            var result = CardDataMapper.MergeMissingSetPrintings(primaryCards, supplementalCards);

            Assert.AreEqual(2, result);
        }

        [TestMethod]
        public void MergeMissingSetPrintings_PrimaryCardHasNullCardSets_AddsFromSupplemental()
        {
            var primaryCards = new List<Card> { new() { ID = 1, Name = "Dark Magician", CardSets = null } };
            var supplementalCards = new List<Card> { new() { ID = 1, Name = "Dark Magician", CardSets = [new Set { Code = "LOB-EN005", RarityName = "Ultra Rare" }] } };

            var result = CardDataMapper.MergeMissingSetPrintings(primaryCards, supplementalCards);

            Assert.AreEqual(1, result);
            Assert.AreEqual(1, primaryCards[0].CardSets!.Count);
            Assert.AreEqual("LOB-EN005", primaryCards[0].CardSets![0].Code);
        }

        [TestMethod]
        public void MergeMissingSetPrintings_SupplementalHasNewComboForExistingCard_AddsSetRow()
        {
            var primaryCards = new List<Card> { new() { ID = 1, Name = "Dark Magician", CardSets = [new Set { Code = "LOB-EN005", RarityName = "Ultra Rare" }] } };
            var supplementalCards = new List<Card>
            {
                new()
                {
                    ID = 1,
                    Name = "Dark Magician",
                    CardSets = [new Set { Code = "LOB-EN005", RarityName = "Ultra Rare" }, new Set { Code = "LOB-EN005", RarityName = "Secret Rare" }]
                }
            };

            var result = CardDataMapper.MergeMissingSetPrintings(primaryCards, supplementalCards);

            Assert.AreEqual(1, result);
            Assert.AreEqual(2, primaryCards[0].CardSets!.Count);
            Assert.IsTrue(primaryCards[0].CardSets!.Any(s => s.RarityName == "Secret Rare"));
        }

        [TestMethod]
        public void MergeMissingSetPrintings_SupplementalNameDiffersFromPrimaryName_StillMerges()
        {
            var primaryCards = new List<Card> { new() { ID = 1, Name = "Dark Magician", CardSets = [new Set { Code = "LOB-EN005", RarityName = "Ultra Rare" }] } };
            var supplementalCards = new List<Card>
            {
                new()
                {
                    ID = 1,
                    Name = "Dark Magician (Alt)",
                    CardSets = [new Set { Code = "LOB-EN005", RarityName = "Ultra Rare" }, new Set { Code = "LOB-EN005", RarityName = "Secret Rare" }]
                }
            };

            var result = CardDataMapper.MergeMissingSetPrintings(primaryCards, supplementalCards);

            Assert.AreEqual(1, result);
            Assert.AreEqual(2, primaryCards[0].CardSets!.Count);
        }

        [TestMethod]
        public void MergeMissingSetPrintings_SupplementalRarityIsGarbage_IsFiltered()
        {
            var primaryCards = new List<Card> { new() { ID = 1, Name = "Some Card", CardSets = [new Set { Code = "ABC-EN001", RarityName = "Common" }] } };
            var supplementalCards = new List<Card>
            {
                new() { ID = 1, Name = "Some Card", CardSets = [new Set { Code = "ABC-EN001", RarityName = "Common" }, new Set { Code = "ABC-EN001", RarityName = "2" }] }
            };

            var result = CardDataMapper.MergeMissingSetPrintings(primaryCards, supplementalCards);

            Assert.AreEqual(0, result);
            Assert.AreEqual(1, primaryCards[0].CardSets!.Count);
        }

        [TestMethod]
        public void MergeMissingSetPrintings_SupplementalSetIsSpeedDuel_IsFiltered()
        {
            var primaryCards = new List<Card> { new() { ID = 1, Name = "Some Card", CardSets = [] } };
            var supplementalCards = new List<Card>
            {
                new() { ID = 1, Name = "Some Card", CardSets = [new Set { Code = "SBLS-EN001", Name = "Speed Duel: Ultimate Predators", RarityName = "Common" }] }
            };

            var result = CardDataMapper.MergeMissingSetPrintings(primaryCards, supplementalCards);

            Assert.AreEqual(0, result);
            Assert.AreEqual(0, primaryCards[0].CardSets!.Count);
        }

        [TestMethod]
        public void StripPlaceholderSets_CardHasNullCardSets_IsSkipped()
        {
            var cards = new List<Card> { new() { ID = 1, CardSets = null } };

            var result = CardDataMapper.StripPlaceholderSets(cards);

            Assert.AreEqual(0, result);
            Assert.IsNull(cards[0].CardSets);
        }

        [TestMethod]
        public void StripPlaceholderSets_CardHasOnlyPlaceholderCodes_LeavesEmptyCardSets()
        {
            var cards = new List<Card>
            {
                new() { ID = 1, CardSets = [new Set { Code = "MAMS-EN???", RarityName = "Ultra Rare" }, new Set { Code = "MAMS-EN???", RarityName = "Secret Rare" }] }
            };

            var result = CardDataMapper.StripPlaceholderSets(cards);

            Assert.AreEqual(2, result);
            Assert.AreEqual(0, cards[0].CardSets!.Count);
        }

        [TestMethod]
        public void StripPlaceholderSets_NoPlaceholderCodes_LeavesCardSetsUnchanged()
        {
            var cards = new List<Card>
            {
                new() { ID = 1, CardSets = [new Set { Code = "MAMO-EN003", RarityName = "Ultra Rare" }, new Set { Code = "BLAR-EN10K", RarityName = "Common" }] }
            };

            var result = CardDataMapper.StripPlaceholderSets(cards);

            Assert.AreEqual(0, result);
            Assert.AreEqual(2, cards[0].CardSets!.Count);
        }

        [TestMethod]
        [DataRow("MAMO-EN0??", DisplayName = "Trailing two placeholder digits")]
        [DataRow("MAMS-EN???", DisplayName = "All three placeholder digits")]
        public void StripPlaceholderSets_PlaceholderCodeWithRealTwin_RemovesOnlyThePlaceholder(string placeholderCode)
        {
            var cards = new List<Card>
            {
                new() { ID = 1, CardSets = [new Set { Code = placeholderCode, RarityName = "Secret Rare" }, new Set { Code = "MAMO-EN022", RarityName = "Secret Rare" }] }
            };

            var result = CardDataMapper.StripPlaceholderSets(cards);

            Assert.AreEqual(1, result);
            Assert.AreEqual(1, cards[0].CardSets!.Count);
            Assert.AreEqual("MAMO-EN022", cards[0].CardSets![0].Code);
        }

        [TestMethod]
        public void StripPlaceholderSets_SetHasNullCode_IsKept()
        {
            var cards = new List<Card> { new() { ID = 1, CardSets = [new Set { Code = null, RarityName = "Common" }] } };

            var result = CardDataMapper.StripPlaceholderSets(cards);

            Assert.AreEqual(0, result);
            Assert.AreEqual(1, cards[0].CardSets!.Count);
        }
    }
}
