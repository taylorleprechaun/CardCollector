using CardCollector.Data.Models.Catalog;
using CardCollector.Models.Legacy;
using CardCollector.Rules.Catalog;
using static CardCollector.Tests.TestHelpers.CatalogTestData;

namespace CardCollector.Tests.Rules.Catalog
{
    [TestClass]
    public sealed class LegacyRowMapperTests
    {
        private static readonly IReadOnlyDictionary<(string SetCode, string RarityName), string> NoCorrections =
            new Dictionary<(string, string), string>();

        [TestMethod]
        public void MapCard_ValueClaimedByTwoCards_ReturnsStrongerClaim()
        {
            var byArtwork = Card(1, "Synthetic Art Holder", aliases: (CatalogSource.YGOProDeck, CatalogAliasKind.Artwork, 55555555));
            var byPasscode = Card(2, "Synthetic Owner", aliases: (CatalogSource.YamlYugi, CatalogAliasKind.Passcode, 55555555));
            var index = LegacyRowMapper.BuildIndex(State(byArtwork, byPasscode), NoCorrections);

            var cardID = LegacyRowMapper.MapCard(index, 55555555);

            Assert.AreEqual(2, cardID);
        }

        [TestMethod]
        public void MapCard_UnknownPasscode_ReturnsNull()
        {
            var index = LegacyRowMapper.BuildIndex(State(Card(1, "Synthetic Card", passcode: 11111111)), NoCorrections);

            var cardID = LegacyRowMapper.MapCard(index, 99999999);

            Assert.IsNull(cardID);
        }

        [TestMethod]
        public void MapPrinting_CorrectedRarityWithRestatedOriginalVariant_DropsVariant()
        {
            var card = Card(1, "Synthetic Fairy", passcode: 11111111);
            var rare = Printing(card, 10, "BP03-EN076", "Rare");
            var corrections = new Dictionary<(string, string), string> { [("BP03-EN076", "SHATTERFOIL RARE")] = "Rare" };
            var index = LegacyRowMapper.BuildIndex(State(card), corrections);

            var match = LegacyRowMapper.MapPrinting(index, 11111111, "BP03-EN076", "Shatterfoil Rare", "Shatterfoil");

            Assert.AreEqual(rare.ID, match.Target!.PrintingID);
            Assert.AreEqual(LegacyMappingRule.Exact, match.Rule);
        }

        [TestMethod]
        public void MapPrinting_ExactMatch_ReturnsPrinting()
        {
            var card = Card(1, "Synthetic Dragon", passcode: 11111111);
            var printing = Printing(card, 10, "SYN-EN001", "Ultra Rare");
            var index = LegacyRowMapper.BuildIndex(State(card), NoCorrections);

            var match = LegacyRowMapper.MapPrinting(index, 11111111, "SYN-EN001", "Ultra Rare", null);

            Assert.AreEqual(printing.ID, match.Target!.PrintingID);
            Assert.AreEqual(LegacyMappingRule.Exact, match.Rule);
        }

        [TestMethod]
        public void MapPrinting_PasscodeSwappedUpstream_FollowsThePrintingsOwner()
        {
            var theory = Card(10, "Synthetic Theory", 21466, 74733322,
                (CatalogSource.YamlYugi, CatalogAliasKind.Passcode, 74733322), (CatalogSource.YGOProDeck, CatalogAliasKind.YGOProDeckID, 48739627));
            var academy = Card(11, "Synthetic Academy", 21465, 48739627,
                (CatalogSource.YamlYugi, CatalogAliasKind.Passcode, 48739627), (CatalogSource.YGOProDeck, CatalogAliasKind.YGOProDeckID, 74733322));
            var theoryPrinting = Printing(theory, 20, "BLGG-EN127", "Ultra Rare");
            Printing(academy, 21, "DUAD-EN054", "Secret Rare");
            var index = LegacyRowMapper.BuildIndex(State(theory, academy), NoCorrections);

            var match = LegacyRowMapper.MapPrinting(index, 48739627, "BLGG-EN127", "Ultra Rare", null);

            Assert.AreEqual(theoryPrinting.ID, match.Target!.PrintingID);
            Assert.AreEqual(10, match.Target.CardID);
        }

        [TestMethod]
        public void MapPrinting_RowCardIDUnknown_CorrectsCardFromSetCode()
        {
            var card = Card(7, "Synthetic Beast", 23513, 52245126, (CatalogSource.YamlYugi, CatalogAliasKind.Passcode, 52245126));
            var printing = Printing(card, 30, "BETB-EN081", "Ultra Rare");
            var index = LegacyRowMapper.BuildIndex(State(card), NoCorrections);

            var match = LegacyRowMapper.MapPrinting(index, 101402101, "BETB-EN081", "Ultra Rare", null);

            Assert.AreEqual(printing.ID, match.Target!.PrintingID);
            Assert.AreEqual(LegacyMappingRule.CardCorrected, match.Rule);
        }

        [TestMethod]
        public void MapPrinting_RowWithoutVariantAndOnlyVariantPrinting_FillsVariant()
        {
            var card = Card(1, "Synthetic Dragon", passcode: 21637210);
            var extendedArt = Printing(card, 10, "RA05-EN146", "Ultra Rare", "Extended Art");
            var index = LegacyRowMapper.BuildIndex(State(card), NoCorrections);

            var match = LegacyRowMapper.MapPrinting(index, 21637210, "RA05-EN146", "Ultra Rare", null);

            Assert.AreEqual(extendedArt.ID, match.Target!.PrintingID);
            Assert.AreEqual(LegacyMappingRule.VariantFilled, match.Rule);
        }

        [TestMethod]
        public void MapPrinting_RowWithoutVariantAndSeveralVariantPrintings_IsUnmapped()
        {
            var card = Card(1, "Synthetic Duelist", passcode: 37440988);
            Printing(card, 10, "LDS1-EN101", "Ultra Rare", "Blue");
            Printing(card, 11, "LDS1-EN101", "Ultra Rare", "Green");
            var index = LegacyRowMapper.BuildIndex(State(card), NoCorrections);

            var match = LegacyRowMapper.MapPrinting(index, 37440988, "LDS1-EN101", "Ultra Rare", null);

            Assert.IsNull(match.Target);
            Assert.AreEqual(LegacyMappingRule.Unmapped, match.Rule);
        }

        [TestMethod]
        public void MapPrinting_SetCodeUnknown_IsUnmappedWithReason()
        {
            var index = LegacyRowMapper.BuildIndex(State(Card(1, "Synthetic Dragon", passcode: 11111111)), NoCorrections);

            var match = LegacyRowMapper.MapPrinting(index, 11111111, "GONE-EN001", "Rare", null);

            Assert.AreEqual(LegacyMappingRule.Unmapped, match.Rule);
            StringAssert.Contains(match.Reason, "GONE-EN001");
        }

        [TestMethod]
        public void MapPrinting_VariantRestatingRarity_MapsToPlainPrinting()
        {
            var card = Card(1, "Synthetic Dragon", passcode: 64335805);
            var plain = Printing(card, 10, "PP01-EN015", "Secret Rare");
            var index = LegacyRowMapper.BuildIndex(State(card), NoCorrections);

            var match = LegacyRowMapper.MapPrinting(index, 64335805, "PP01-EN015", "Secret Rare", "Secret");

            Assert.AreEqual(plain.ID, match.Target!.PrintingID);
        }
    }
}
