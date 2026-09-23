using CardCollector.DTO;
using CardCollector.Rules;
using CardCollector.ViewModels;

namespace CardCollector.Tests.Rules
{
    [TestClass]
    public sealed class DeckSorterTests
    {
        [TestMethod]
        public void Sort_ExtraDeckCards_AreFusionThenSynchroThenXyzThenLink()
        {
            var cards = new[]
            {
                Row(1, "Link Monster", "Alpha"),
                Row(2, "Xyz Monster", "Alpha"),
                Row(3, "Synchro Monster", "Alpha"),
                Row(4, "Fusion Monster", "Alpha")
            };

            var sorted = DeckSorter.Sort(cards);

            CollectionAssert.AreEqual(new[] { 4, 3, 2, 1 }, sorted.Select(c => c.CardID).ToArray());
        }

        [TestMethod]
        public void Sort_MainDeckCards_AreMonstersThenSpellsThenTraps()
        {
            var cards = new[]
            {
                Row(1, "Trap Card", "Alpha"),
                Row(2, "Spell Card", "Alpha"),
                Row(3, "Effect Monster", "Zulu"),
                Row(4, "Normal Monster", "Bravo")
            };

            var sorted = DeckSorter.Sort(cards);

            CollectionAssert.AreEqual(new[] { 4, 3, 2, 1 }, sorted.Select(c => c.CardID).ToArray());
        }

        [TestMethod]
        public void Sort_NullCards_ThrowsArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => DeckSorter.Sort(null!));
        }

        [TestMethod]
        public void Sort_PendulumAndDifferentlyCasedTypes_LandInTheirExtraDeckGroup()
        {
            var cards = new[]
            {
                Row(1, "XYZ Pendulum Effect Monster", "Alpha"),
                Row(2, "Synchro Pendulum Effect Monster", "Alpha"),
                Row(3, "Pendulum Effect Fusion Monster", "Alpha"),
                Row(4, "XYZ Monster", "Bravo"),
                Row(5, "Pendulum Effect Monster", "Alpha")
            };

            var sorted = DeckSorter.Sort(cards);

            CollectionAssert.AreEqual(new[] { 5, 3, 2, 1, 4 }, sorted.Select(c => c.CardID).ToArray());
        }

        [TestMethod]
        public void Sort_SameNameAndType_OrdersByCardID()
        {
            var cards = new[] { Row(9, "Effect Monster", "Alpha"), Row(3, "Effect Monster", "Alpha") };

            var sorted = DeckSorter.Sort(cards);

            CollectionAssert.AreEqual(new[] { 3, 9 }, sorted.Select(c => c.CardID).ToArray());
        }

        [TestMethod]
        public void Sort_SideDeck_IsMainMonstersThenExtraMonstersThenSpellsThenTraps()
        {
            var cards = new[]
            {
                Row(1, "Trap Card", "Alpha"),
                Row(2, "Spell Card", "Alpha"),
                Row(3, "Link Monster", "Alpha"),
                Row(4, "Fusion Monster", "Alpha"),
                Row(5, "Effect Monster", "Zulu"),
                Row(6, "Xyz Monster", "Alpha"),
                Row(7, "Synchro Monster", "Alpha"),
                Row(8, "Normal Monster", "Alpha")
            };

            var sorted = DeckSorter.Sort(cards);

            CollectionAssert.AreEqual(new[] { 8, 5, 4, 7, 6, 3, 2, 1 }, sorted.Select(c => c.CardID).ToArray());
        }

        [TestMethod]
        public void Sort_UnknownCards_GoLastOrderedByPasscode()
        {
            var cards = new[]
            {
                new DeckCardViewModel { Card = null, CardID = 700, Quantity = 1 },
                Row(1, "Trap Card", "Alpha"),
                new DeckCardViewModel { Card = null, CardID = 600, Quantity = 1 }
            };

            var sorted = DeckSorter.Sort(cards);

            CollectionAssert.AreEqual(new[] { 1, 600, 700 }, sorted.Select(c => c.CardID).ToArray());
        }

        [TestMethod]
        public void Sort_WithinAType_OrdersByNameIgnoringCase()
        {
            var cards = new[]
            {
                Row(1, "Effect Monster", "zebra"),
                Row(2, "Effect Monster", "Apple"),
                Row(3, "Effect Monster", "banana")
            };

            var sorted = DeckSorter.Sort(cards);

            CollectionAssert.AreEqual(new[] { 2, 3, 1 }, sorted.Select(c => c.CardID).ToArray());
        }

        private static DeckCardViewModel Row(int cardID, string cardType, string name) =>
            new() { Card = new Card { CardType = cardType, ID = cardID, Name = name }, CardID = cardID, Quantity = 1 };
    }
}
