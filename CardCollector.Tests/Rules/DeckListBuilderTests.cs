using CardCollector.Data.Models;
using CardCollector.Models;
using CardCollector.Rules;

namespace CardCollector.Tests.Rules
{
    [TestClass]
    public sealed class DeckListBuilderTests
    {
        private static readonly IReadOnlyDictionary<int, int> NoAliases = new Dictionary<int, int>();

        [TestMethod]
        public void Build_AliasedPasscodes_AreResolvedThenAddedTogether()
        {
            var deck = Deck(main: [83764718, 83764719, 83764719]);
            var aliases = new Dictionary<int, int> { [83764719] = 83764718 };

            var rows = DeckListBuilder.Build(deck, aliases);

            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual(83764718, rows[0].CardID);
            Assert.AreEqual(3, rows[0].Quantity);
        }

        [TestMethod]
        public void Build_CopiesOfOneCard_AreCollapsedIntoAQuantity()
        {
            var deck = Deck(main: [100, 100, 100, 200]);

            var rows = DeckListBuilder.Build(deck, NoAliases);

            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual(3, rows.Single(r => r.CardID == 100).Quantity);
            Assert.AreEqual(1, rows.Single(r => r.CardID == 200).Quantity);
        }

        [TestMethod]
        public void Build_NullArguments_ThrowArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => DeckListBuilder.Build(null!, NoAliases));
            Assert.ThrowsExactly<ArgumentNullException>(() => DeckListBuilder.Build(Deck(main: [1]), null!));
        }

        [TestMethod]
        public void Build_SameCardInTwoSections_KeepsARowPerSection()
        {
            var deck = Deck(main: [100, 100], side: [100]);

            var rows = DeckListBuilder.Build(deck, NoAliases);

            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual(2, rows.Single(r => r.Section == DeckSection.Main).Quantity);
            Assert.AreEqual(1, rows.Single(r => r.Section == DeckSection.Side).Quantity);
        }

        [TestMethod]
        public void Build_SortOrder_IsWhereTheCardFirstAppearedInItsSection()
        {
            var deck = Deck(main: [300, 100, 300, 200], extra: [900, 800]);

            var rows = DeckListBuilder.Build(deck, NoAliases);

            Assert.AreEqual(0, rows.Single(r => r.CardID == 300).SortOrder);
            Assert.AreEqual(1, rows.Single(r => r.CardID == 100).SortOrder);
            Assert.AreEqual(3, rows.Single(r => r.CardID == 200).SortOrder);
            Assert.AreEqual(0, rows.Single(r => r.CardID == 900).SortOrder);
            Assert.AreEqual(1, rows.Single(r => r.CardID == 800).SortOrder);
        }

        [TestMethod]
        public void Build_UnknownPasscode_IsKeptAsPasted()
        {
            var deck = Deck(main: [424242]);

            var rows = DeckListBuilder.Build(deck, NoAliases);

            Assert.AreEqual(424242, rows.Single().CardID);
        }

        private static ParsedDeckList Deck(int[]? main = null, int[]? extra = null, int[]? side = null) =>
            new()
            {
                Extra = extra ?? [],
                Main = main ?? [],
                Side = side ?? []
            };
    }
}
