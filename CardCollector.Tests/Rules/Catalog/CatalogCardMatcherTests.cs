using CardCollector.Data.Models.Catalog;
using CardCollector.Models.Catalog;
using CardCollector.Rules.Catalog;
using static CardCollector.Tests.TestHelpers.CatalogTestData;

namespace CardCollector.Tests.Rules.Catalog
{
    [TestClass]
    public sealed class CatalogCardMatcherTests
    {
        [TestMethod]
        public void Combine_KonamiIDsDiffer_RefusesPasscodePairing()
        {
            var yaml = YamlCard("Synthetic Spell", konamiID: 100, passcode: 11111111);
            var ygo = YGOProDeckCard("Synthetic Trap", 11111111, konamiID: 200);

            var observed = CatalogCardMatcher.Combine([yaml], [ygo]);

            Assert.AreEqual(2, observed.Count);
            Assert.AreEqual(0, observed[0].YGOProDeck.Count);
        }

        [TestMethod]
        public void Combine_PasscodesSwappedBetweenSources_PairsByKonamiID()
        {
            var yamlTheorealize = YamlCard("Synthetic Theory", konamiID: 21466, passcode: 74733322);
            var yamlArtmage = YamlCard("Synthetic Academy", konamiID: 21465, passcode: 48739627);
            var ygoTheorealize = YGOProDeckCard("Synthetic Theory", 48739627, konamiID: 21466);
            var ygoArtmage = YGOProDeckCard("Synthetic Academy", 74733322, konamiID: 21465);

            var observed = CatalogCardMatcher.Combine([yamlTheorealize, yamlArtmage], [ygoArtmage, ygoTheorealize]);

            Assert.AreEqual(2, observed.Count);
            Assert.AreEqual(48739627, observed.Single(o => o.Name == "Synthetic Theory").Images[0].ID);
            Assert.AreEqual(74733322, observed.Single(o => o.Name == "Synthetic Academy").Images[0].ID);
        }

        [TestMethod]
        public void Combine_UnpairedYGOProDeckCard_BecomesItsOwnObservedCard()
        {
            var yaml = YamlCard("Synthetic Spell", konamiID: 100, passcode: 11111111);
            var ygo = YGOProDeckCard("Synthetic Token", 99999999);

            var observed = CatalogCardMatcher.Combine([yaml], [ygo]);

            Assert.AreEqual(2, observed.Count);
            Assert.IsNull(observed[1].YamlYugi);
            Assert.AreEqual("Synthetic Token", observed[1].Name);
        }

        [TestMethod]
        public void Combine_YamlDocumentsShareKonamiID_MergesThemIntoOneCard()
        {
            var main = YamlCard("Synthetic Magician", 4041, 46986414, new SourcePrinting("SYN-EN001", "Synthetic Set", "Ultra Rare"), new SourcePrinting("SYN-EN002", "Synthetic Set", "Rare"));
            var alternateArt = YamlCard("Synthetic Magician", 4041, 36996508, new SourcePrinting("ALT-EN001", "Alt Set", "Secret Rare"));

            var observed = CatalogCardMatcher.Combine([alternateArt, main], []).Single();

            Assert.AreEqual(46986414, observed.PreferredPasscode);
            Assert.AreEqual(3, observed.Printings.Count);
            CollectionAssert.Contains(observed.Aliases.Select(a => a.Value).ToList(), 36996508);
        }

        [TestMethod]
        public void Combine_YGOProDeckCardWithoutKonamiID_PairsByUniqueSetCode()
        {
            var yaml = YamlCard("Synthetic Spell", konamiID: 100, passcode: 11111111, new SourcePrinting("SYN-EN001", "Synthetic Set", "Common"));
            var ygo = YGOProDeckCard("Synthetic Spell (Alt)", 22222222, null, null, new SourcePrinting("SYN-EN001", "Synthetic Set", "Common"));

            var observed = CatalogCardMatcher.Combine([yaml], [ygo]).Single();

            Assert.AreEqual(1, observed.YGOProDeck.Count);
        }

        [TestMethod]
        public void Combine_YGOProDeckIDEqualsYamlFakePassword_Pairs()
        {
            var yamlWithFake = new SourceCard { FakePassword = 10000000, Name = "Synthetic God", Source = CatalogSource.YamlYugi };
            var ygo = YGOProDeckCard("Synthetic God", 10000000);

            var observed = CatalogCardMatcher.Combine([yamlWithFake], [ygo]).Single();

            Assert.AreEqual(1, observed.YGOProDeck.Count);
            Assert.AreEqual(10000000, observed.PreferredPasscode);
        }

        [TestMethod]
        public void Match_AliasClaimedByAnotherObservedCard_ReportsConflict()
        {
            var catalogCard = Card(1, "Synthetic Spell", passcode: 11111111, aliases: (CatalogSource.YamlYugi, CatalogAliasKind.Passcode, 11111111));
            var first = new ObservedCard(YamlCard("Synthetic Spell", passcode: 11111111), []);
            var second = new ObservedCard(null, [YGOProDeckCard("Synthetic Spell Copy", 11111111)]);

            var result = CatalogCardMatcher.Match([first, second], [catalogCard]);

            Assert.AreSame(catalogCard, result.Matches.Single().Card);
            Assert.AreSame(second, result.Conflicts.Single().Observed);
        }

        [TestMethod]
        public void Match_BetaIDBecameRealPasscode_MatchesCardCreatedFromBetaID()
        {
            var catalogCard = Card(7, "Synthetic Beast", aliases: (CatalogSource.YGOProDeck, CatalogAliasKind.YGOProDeckID, 101402101));
            var observed = new ObservedCard(
                YamlCard("Synthetic Beast", konamiID: 23513, passcode: 52245126),
                [YGOProDeckCard("Synthetic Beast", 52245126, konamiID: 23513, betaID: 101402101)]);

            var result = CatalogCardMatcher.Match([observed], [catalogCard]);

            Assert.AreSame(catalogCard, result.Matches.Single().Card);
        }

        [TestMethod]
        public void Match_CatalogCardHasDifferentKonamiID_IsNotChosenByAlias()
        {
            var catalogCard = Card(1, "Synthetic Spell", konamiID: 100, aliases: (CatalogSource.YamlYugi, CatalogAliasKind.Passcode, 11111111));
            var observed = new ObservedCard(YamlCard("Synthetic Trap", konamiID: 200, passcode: 11111111), []);

            var result = CatalogCardMatcher.Match([observed], [catalogCard]);

            Assert.IsNull(result.Matches.Single().Card);
        }

        [TestMethod]
        public void Match_NameOnlyPreAnnouncedCard_MatchesByName()
        {
            var catalogCard = Card(3, "Synthetic Announcement");
            var observed = new ObservedCard(YamlCard("Synthetic Announcement", konamiID: 30000), []);

            var result = CatalogCardMatcher.Match([observed], [catalogCard]);

            Assert.AreSame(catalogCard, result.Matches.Single().Card);
        }

        [TestMethod]
        public void Match_PasscodeSwappedUpstream_KeepsEachCardByKonamiID()
        {
            var theorealize = Card(10, "Synthetic Theory", 21466, 48739627, (CatalogSource.YamlYugi, CatalogAliasKind.Passcode, 48739627));
            var artmage = Card(11, "Synthetic Academy", 21465, 74733322, (CatalogSource.YamlYugi, CatalogAliasKind.Passcode, 74733322));
            var observedTheorealize = new ObservedCard(YamlCard("Synthetic Theory", konamiID: 21466, passcode: 74733322), []);
            var observedArtmage = new ObservedCard(YamlCard("Synthetic Academy", konamiID: 21465, passcode: 48739627), []);

            var result = CatalogCardMatcher.Match([observedArtmage, observedTheorealize], [theorealize, artmage]);

            Assert.AreSame(artmage, result.Matches.Single(m => m.Observed == observedArtmage).Card);
            Assert.AreSame(theorealize, result.Matches.Single(m => m.Observed == observedTheorealize).Card);
        }

        [TestMethod]
        public void Match_SharedAliasValue_PrefersStrongerAliasKind()
        {
            var byArtwork = Card(1, "Synthetic Art Holder", aliases: (CatalogSource.YGOProDeck, CatalogAliasKind.Artwork, 55555555));
            var byPasscode = Card(2, "Synthetic Owner", aliases: (CatalogSource.YamlYugi, CatalogAliasKind.Passcode, 55555555));
            var observed = new ObservedCard(YamlCard("Synthetic Owner", passcode: 55555555), []);

            var result = CatalogCardMatcher.Match([observed], [byArtwork, byPasscode]);

            Assert.AreSame(byPasscode, result.Matches.Single().Card);
        }

        [TestMethod]
        public void Match_UnknownCard_ReturnsNullCard()
        {
            var observed = new ObservedCard(YamlCard("Synthetic Newcomer", konamiID: 99999, passcode: 12345678), []);

            var result = CatalogCardMatcher.Match([observed], [Card(1, "Synthetic Other", konamiID: 1)]);

            Assert.IsNull(result.Matches.Single().Card);
            Assert.AreEqual(0, result.Conflicts.Count);
        }
    }
}
