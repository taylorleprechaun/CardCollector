using CardCollector.DTO;
using CardCollector.DTO.YamlYugi;
using CardCollector.DTO.YGOProDeck;
using CardCollector.Rules.Catalog;

namespace CardCollector.Tests.Rules.Catalog
{
    [TestClass]
    public sealed class CatalogSourceNormalizerTests
    {
        private static readonly IReadOnlyDictionary<(string SetCode, string RarityName), string> NoCorrections =
            new Dictionary<(string, string), string>();

        [TestMethod]
        public void FromTCGPlayer_OneProductWithTwoEditions_ReturnsOneProduct()
        {
            var rows = new List<TCGPriceSet>
            {
                new() { CardName = "Synthetic Dragon", Code = "SYN-EN001", Edition = "1st Edition", ProductID = 500, RarityName = "Ultra Rare" },
                new() { CardName = "Synthetic Dragon", Code = "SYN-EN001", Edition = "Unlimited", ProductID = 500, RarityName = "Ultra Rare" }
            };

            var products = CatalogSourceNormalizer.FromTCGPlayer(rows, NoCorrections);

            Assert.AreEqual(500, products.Single().ProductID);
        }

        [TestMethod]
        public void FromTCGPlayer_RarityInCorrectionTable_IsCorrected()
        {
            var rows = new List<TCGPriceSet> { new() { CardName = "Synthetic Fairy", Code = "BP03-EN076", ProductID = 500, RarityName = "Shatterfoil Rare" } };
            var corrections = new Dictionary<(string, string), string> { [("BP03-EN076", "SHATTERFOIL RARE")] = "Rare" };

            var products = CatalogSourceNormalizer.FromTCGPlayer(rows, corrections);

            Assert.AreEqual("Rare", products.Single().RarityName);
        }

        [TestMethod]
        public void FromTCGPlayer_RowWithoutProductID_IsSkipped()
        {
            var rows = new List<TCGPriceSet> { new() { CardName = "Synthetic Dragon", Code = "SYN-EN001", RarityName = "Ultra Rare" } };

            var products = CatalogSourceNormalizer.FromTCGPlayer(rows, NoCorrections);

            Assert.AreEqual(0, products.Count);
        }

        [TestMethod]
        public void FromTCGPlayer_ShortPrintRarity_IsNormalizedToCommon()
        {
            var rows = new List<TCGPriceSet> { new() { CardName = "Synthetic Dragon", Code = "SYN-EN001", ProductID = 500, RarityName = "Short Print" } };

            var products = CatalogSourceNormalizer.FromTCGPlayer(rows, NoCorrections);

            Assert.AreEqual("Common", products.Single().RarityName);
        }

        [TestMethod]
        public void FromYamlYugi_CardWithoutPasscode_IsKept()
        {
            var documents = new List<YamlCard> { new() { FakePassword = 10000000, KonamiID = 4998, Name = new YamlLocalizedString { En = "Synthetic God" } } };

            var card = CatalogSourceNormalizer.FromYamlYugi(documents, NoCorrections).Single();

            Assert.IsNull(card.Passcode);
            Assert.AreEqual(10000000, card.FakePassword);
        }

        [TestMethod]
        public void FromYamlYugi_FiltersAndCorrectsPrintings()
        {
            var documents = new List<YamlCard>
            {
                new()
                {
                    Name = new YamlLocalizedString { En = "Synthetic Dragon" },
                    Sets = new YamlCardSets
                    {
                        En =
                        [
                            new() { SetNumber = "SYN-EN001", SetName = "Synthetic Set", Rarities = ["Ultra Rare (Pharaoh's Rare)"] },
                            new() { SetNumber = "SYN-EN???", SetName = "Synthetic Set", Rarities = ["Common"] },
                            new() { SetNumber = "SS01-ENA01", SetName = "Speed Duel: Synthetic", Rarities = ["Common"] }
                        ]
                    }
                }
            };
            var corrections = new Dictionary<(string, string), string> { [("SYN-EN001", "ULTRA RARE (PHARAOH'S RARE)")] = "Ultra Pharaoh’s Rare" };

            var card = CatalogSourceNormalizer.FromYamlYugi(documents, corrections).Single();

            Assert.AreEqual("Ultra Pharaoh’s Rare", card.Printings.Single().RarityName);
        }

        [TestMethod]
        public void FromYamlYugi_NoEnglishName_IsSkipped()
        {
            var documents = new List<YamlCard> { new() { KonamiID = 1, Password = 11111111 } };

            var cards = CatalogSourceNormalizer.FromYamlYugi(documents, NoCorrections);

            Assert.AreEqual(0, cards.Count);
        }

        [TestMethod]
        public void FromYGOProDeck_CopiesIdentifiersAndDropsUnknownRarities()
        {
            var cards = new List<YGOProDeckCard>
            {
                new()
                {
                    CardImages = [new Image { ID = 46986414 }, new Image { ID = 46986415 }],
                    CardSets = [new Set { Code = "SYN-EN001", Name = "Synthetic Set", RarityName = "Ultra Rare" }, new Set { Code = "SYN-EN002", Name = "Synthetic Set", RarityName = "2" }],
                    ID = 46986414,
                    KonamiID = 4041,
                    LinkMarkers = ["Left", "Bottom-Right"],
                    MiscInfo = [new YGOProDeckMiscInfo { BetaID = 101000001, KonamiID = 4041 }],
                    Name = "Synthetic Magician"
                }
            };

            var card = CatalogSourceNormalizer.FromYGOProDeck(cards, NoCorrections).Single();

            Assert.AreEqual(46986414, card.YGOProDeckID);
            Assert.AreEqual(101000001, card.BetaID);
            CollectionAssert.AreEqual(new[] { 46986415 }, card.ArtworkIDs.ToArray());
            Assert.AreEqual("Left,Bottom-Right", card.LinkMarkers);
            Assert.AreEqual("SYN-EN001", card.Printings.Single().SetCode);
        }
    }
}
