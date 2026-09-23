using CardCollector.Data.Models;
using CardCollector.DTO;
using CardCollector.Rules;
using CardCollector.ViewModels;

namespace CardCollector.Tests.Rules
{
    [TestClass]
    public sealed class DeckRulesTests
    {
        [TestMethod]
        public void CountTypes_MixedCards_CountsCopiesByType()
        {
            var cards = new[]
            {
                Row("Effect Monster", 3),
                Row("Normal Monster", 1),
                Row("Spell Card", 2),
                Row("Trap Card", 4)
            };

            var counts = DeckRules.CountTypes(cards);

            Assert.AreEqual(new DeckTypeCounts(4, 2, 4), counts);
        }

        [TestMethod]
        public void CountTypes_NullCards_ThrowsArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => DeckRules.CountTypes(null!));
        }

        [TestMethod]
        public void CountTypes_UnknownCard_IsSkipped()
        {
            var cards = new[]
            {
                new DeckCardViewModel { Card = null, CardID = 999, Quantity = 2 },
                Row("Spell Card", 1)
            };

            var counts = DeckRules.CountTypes(cards);

            Assert.AreEqual(new DeckTypeCounts(0, 1, 0), counts);
        }

        [TestMethod]
        public void Normalize_BlankNotes_BecomeNull()
        {
            var normalized = DeckRules.Normalize(new Deck { Name = "Sample", Notes = "   " });

            Assert.IsNull(normalized.Notes);
        }

        [TestMethod]
        public void Normalize_NameAndNotes_AreTrimmed()
        {
            var normalized = DeckRules.Normalize(new Deck { ID = 4, Name = "  Sample Deck ", Notes = " a note " });

            Assert.AreEqual(4, normalized.ID);
            Assert.AreEqual("Sample Deck", normalized.Name);
            Assert.AreEqual("a note", normalized.Notes);
        }

        [TestMethod]
        public void Normalize_NullDeck_ThrowsArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => DeckRules.Normalize(null!));
        }

        [TestMethod]
        public void Validate_BlankName_ReturnsError()
        {
            var errors = DeckRules.Validate(new Deck { Name = "  " });

            CollectionAssert.AreEqual(new[] { "Deck name is required." }, errors.ToList());
        }

        [TestMethod]
        public void Validate_NameOverLimit_ReturnsError()
        {
            var errors = DeckRules.Validate(new Deck { Name = new string('x', DeckRules.MAX_NAME_LENGTH + 1) });

            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains(errors[0], "150");
        }

        [TestMethod]
        public void Validate_NameWithinLimit_ReturnsNoErrors()
        {
            var errors = DeckRules.Validate(new Deck { Name = new string('x', DeckRules.MAX_NAME_LENGTH) });

            Assert.AreEqual(0, errors.Count);
        }

        [TestMethod]
        public void Validate_NullDeck_ThrowsArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => DeckRules.Validate(null!));
        }

        private static DeckCardViewModel Row(string cardType, int quantity) =>
            new() { Card = new Card { CardType = cardType }, CardID = 1, Quantity = quantity };
    }
}
