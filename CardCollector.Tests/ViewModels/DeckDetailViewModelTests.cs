using CardCollector.Data.Models;
using CardCollector.DTO;
using CardCollector.ViewModels;

namespace CardCollector.Tests.ViewModels
{
    [TestClass]
    public sealed class DeckDetailViewModelTests
    {
        [TestMethod]
        public void AllCards_EmptyDeck_IsEmpty()
        {
            var detail = BuildDetail(main: [], extra: [], side: []);

            Assert.AreEqual(0, detail.AllCards.Count);
        }

        [TestMethod]
        public void AllCards_EverySection_ListsMainThenExtraThenSide()
        {
            var detail = BuildDetail(main: [BuildCard(1)], extra: [BuildCard(2)], side: [BuildCard(3), BuildCard(4)]);

            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, detail.AllCards.Select(c => c.CardID).ToArray());
        }

        [TestMethod]
        public void UnknownCardCount_UnknownCardsInSeveralSections_CountsEveryCopy()
        {
            var detail = BuildDetail(
                main: [BuildCard(1, quantity: 2, isKnown: false), BuildCard(2)],
                extra: [BuildCard(3, isKnown: false)],
                side: [BuildCard(4, quantity: 3, isKnown: false)]);

            Assert.AreEqual(6, detail.UnknownCardCount);
        }

        private static DeckCardViewModel BuildCard(int cardID, int quantity = 1, bool isKnown = true) =>
            new()
            {
                Card = isKnown ? new Card { ID = cardID, Name = $"Test Card {cardID}" } : null,
                CardID = cardID,
                Quantity = quantity
            };

        private static DeckDetailViewModel BuildDetail(
            IReadOnlyList<DeckCardViewModel> main,
            IReadOnlyList<DeckCardViewModel> extra,
            IReadOnlyList<DeckCardViewModel> side) =>
            new()
            {
                Deck = new Deck { ID = 1, Name = "Sample Deck" },
                Events = [],
                Extra = new DeckSectionViewModel { Cards = extra },
                Main = new DeckSectionViewModel { Cards = main },
                MainTypes = new DeckTypeCounts(0, 0, 0),
                Side = new DeckSectionViewModel { Cards = side }
            };
    }
}
