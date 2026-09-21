using CardCollector.DTO;
using CardCollector.Services;

namespace CardCollector.Tests.Services
{
    [TestClass]
    public sealed class PasscodeAliasResolverTests
    {
        [TestMethod]
        public void BuildAliases_AlternateArtworkOfYamlCard_MapsToTheYamlCard()
        {
            var yamlCardIDs = new[] { 1000 };
            var ygoProDeckCards = new[] { YgoProDeckCard(1000, 1001, 1002) };

            var aliases = PasscodeAliasResolver.BuildAliases(yamlCardIDs, ygoProDeckCards);

            Assert.AreEqual(1000, aliases[1001]);
            Assert.AreEqual(1000, aliases[1002]);
            Assert.IsFalse(aliases.ContainsKey(1000));
        }

        [TestMethod]
        public void BuildAliases_ArtworkIdThatIsAnotherCardsPrimaryId_IsNotAnAlias()
        {
            var yamlCardIDs = new[] { 1000 };
            var ygoProDeckCards = new[] { YgoProDeckCard(1000, 2000), YgoProDeckCard(2000) };

            var aliases = PasscodeAliasResolver.BuildAliases(yamlCardIDs, ygoProDeckCards);

            Assert.AreEqual(0, aliases.Count);
        }

        [TestMethod]
        public void BuildAliases_ArtworkIdThatIsItselfAYamlCard_IsNotAnAlias()
        {
            var yamlCardIDs = new[] { 1000, 1001 };
            var ygoProDeckCards = new[] { YgoProDeckCard(1000, 1001) };

            var aliases = PasscodeAliasResolver.BuildAliases(yamlCardIDs, ygoProDeckCards);

            Assert.IsFalse(aliases.ContainsKey(1001));
        }

        [TestMethod]
        public void BuildAliases_MonsterRebornWithDifferentPrimaryId_MapsTheYgoProDeckIdToTheYamlId()
        {
            var yamlCardIDs = new[] { 83764718 };
            var ygoProDeckCards = new[] { YgoProDeckCard(83764719, 83764718) };

            var aliases = PasscodeAliasResolver.BuildAliases(yamlCardIDs, ygoProDeckCards);

            Assert.AreEqual(83764718, PasscodeAliasResolver.Resolve(aliases, 83764719));
        }

        [TestMethod]
        public void BuildAliases_NoAlternateIds_ReturnsNoAliases()
        {
            var yamlCardIDs = new[] { 1000 };
            var ygoProDeckCards = new[] { YgoProDeckCard(1000) };

            var aliases = PasscodeAliasResolver.BuildAliases(yamlCardIDs, ygoProDeckCards);

            Assert.AreEqual(0, aliases.Count);
        }

        [TestMethod]
        public void BuildAliases_NullArguments_ThrowArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => PasscodeAliasResolver.BuildAliases(null!, []));
            Assert.ThrowsExactly<ArgumentNullException>(() => PasscodeAliasResolver.BuildAliases([], null!));
        }

        [TestMethod]
        public void BuildAliases_PasscodeInTwoCards_KeepsTheFirstMapping()
        {
            var yamlCardIDs = new[] { 1000, 3000 };
            var ygoProDeckCards = new[] { YgoProDeckCard(1000, 9999), YgoProDeckCard(3000, 9999) };

            var aliases = PasscodeAliasResolver.BuildAliases(yamlCardIDs, ygoProDeckCards);

            Assert.AreEqual(1000, aliases[9999]);
        }

        [TestMethod]
        public void BuildAliases_YgoProDeckOnlyCardWithAlternateArtwork_MapsToItsPrimaryId()
        {
            var yamlCardIDs = new[] { 1000 };
            var ygoProDeckCards = new[] { YgoProDeckCard(5000, 5001) };

            var aliases = PasscodeAliasResolver.BuildAliases(yamlCardIDs, ygoProDeckCards);

            Assert.AreEqual(5000, aliases[5001]);
        }

        [TestMethod]
        public void Resolve_NoAlias_ReturnsThePasscode()
        {
            var aliases = new Dictionary<int, int> { [2] = 1 };

            Assert.AreEqual(7, PasscodeAliasResolver.Resolve(aliases, 7));
        }

        [TestMethod]
        public void Resolve_NullAliases_ThrowsArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => PasscodeAliasResolver.Resolve(null!, 1));
        }

        private static Card YgoProDeckCard(int primaryID, params int[] otherArtworkIDs) =>
            new()
            {
                CardImages = new[] { primaryID }.Concat(otherArtworkIDs).Select(id => new Image { ID = id }).ToList(),
                ID = primaryID
            };
    }
}
