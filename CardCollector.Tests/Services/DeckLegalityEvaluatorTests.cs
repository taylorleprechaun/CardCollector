using CardCollector.DTO;
using CardCollector.Services;
using CardCollector.ViewModels;

namespace CardCollector.Tests.Services
{
    [TestClass]
    public sealed class DeckLegalityEvaluatorTests
    {
        [TestMethod]
        public void Evaluate_CopiesSplitAcrossSections_CountedTogether()
        {
            var card = BuildCard(1, "Pot of Greed", konamiID: 100);
            var cards = new[]
            {
                BuildDeckCard(card, 1, quantity: 1),
                BuildDeckCard(card, 1, quantity: 1)
            };
            var banlist = BuildBanlist((100, BanlistLimit.Limited));

            var result = DeckLegalityEvaluator.Evaluate(cards, banlist);

            Assert.IsFalse(result.IsLegal);
            Assert.AreEqual(2, result.Violations.Single().Copies);
        }

        [TestMethod]
        public void Evaluate_EmptyDeck_ReturnsLegalWithNoStatuses()
        {
            var banlist = BuildBanlist((100, BanlistLimit.Forbidden));

            var result = DeckLegalityEvaluator.Evaluate([], banlist);

            Assert.IsTrue(result.IsLegal);
            Assert.AreEqual(0, result.CardStatuses.Count);
        }

        [TestMethod]
        public void Evaluate_ForbiddenCardPresent_ReportsViolation()
        {
            var card = BuildCard(1, "Pot of Greed", konamiID: 100);
            var cards = new[] { BuildDeckCard(card, 1, quantity: 1) };
            var banlist = BuildBanlist((100, BanlistLimit.Forbidden));

            var result = DeckLegalityEvaluator.Evaluate(cards, banlist);

            Assert.IsFalse(result.IsLegal);
            Assert.AreEqual(BanlistLimit.Forbidden, result.Violations.Single().Limit);
        }

        [TestMethod]
        public void Evaluate_KonamiIDNotOnBanlist_Unrestricted()
        {
            var card = BuildCard(1, "Unlisted Card", konamiID: 500);
            var cards = new[] { BuildDeckCard(card, 1, quantity: 3) };
            var banlist = BuildBanlist((999, BanlistLimit.Forbidden));

            var result = DeckLegalityEvaluator.Evaluate(cards, banlist);

            Assert.IsTrue(result.IsLegal);
            Assert.AreEqual(0, result.CardStatuses.Count);
        }

        [TestMethod]
        public void Evaluate_LegalDeck_StillReportsLimitedStatus()
        {
            var card = BuildCard(1, "Raigeki", konamiID: 200);
            var cards = new[] { BuildDeckCard(card, 1, quantity: 1) };
            var banlist = BuildBanlist((200, BanlistLimit.Limited));

            var result = DeckLegalityEvaluator.Evaluate(cards, banlist);

            Assert.IsTrue(result.IsLegal);
            Assert.AreEqual(BanlistLimit.Limited, result.CardStatuses[1].Limit);
            Assert.IsFalse(result.CardStatuses[1].IsViolation);
        }

        [TestMethod]
        public void Evaluate_LimitedWithOneCopy_NoViolation()
        {
            var card = BuildCard(1, "Raigeki", konamiID: 200);
            var cards = new[] { BuildDeckCard(card, 1, quantity: 1) };
            var banlist = BuildBanlist((200, BanlistLimit.Limited));

            var result = DeckLegalityEvaluator.Evaluate(cards, banlist);

            Assert.IsTrue(result.IsLegal);
        }

        [TestMethod]
        public void Evaluate_LimitedWithTwoCopies_ReportsViolation()
        {
            var card = BuildCard(1, "Raigeki", konamiID: 200);
            var cards = new[] { BuildDeckCard(card, 1, quantity: 2) };
            var banlist = BuildBanlist((200, BanlistLimit.Limited));

            var result = DeckLegalityEvaluator.Evaluate(cards, banlist);

            Assert.IsFalse(result.IsLegal);
            Assert.AreEqual(2, result.Violations.Single().Copies);
        }

        [TestMethod]
        public void Evaluate_MultipleViolations_OrderedByLimitThenName()
        {
            var raigeki = BuildCard(1, "Raigeki", konamiID: 100);
            var potOfGreed = BuildCard(2, "Pot of Greed", konamiID: 200);
            var cards = new[]
            {
                BuildDeckCard(raigeki, 1, quantity: 2),
                BuildDeckCard(potOfGreed, 2, quantity: 1)
            };
            var banlist = BuildBanlist((100, BanlistLimit.Limited), (200, BanlistLimit.Forbidden));

            var result = DeckLegalityEvaluator.Evaluate(cards, banlist);

            Assert.AreEqual(2, result.Violations.Count);
            Assert.AreEqual("Pot of Greed", result.Violations[0].CardName);
            Assert.AreEqual("Raigeki", result.Violations[1].CardName);
        }

        [TestMethod]
        public void Evaluate_NoKonamiID_Unrestricted()
        {
            var card = BuildCard(1, "Ghost Card", konamiID: null);
            var cards = new[] { BuildDeckCard(card, 1, quantity: 3) };
            var banlist = BuildBanlist((999, BanlistLimit.Forbidden));

            var result = DeckLegalityEvaluator.Evaluate(cards, banlist);

            Assert.IsTrue(result.IsLegal);
            Assert.AreEqual(0, result.CardStatuses.Count);
        }

        [TestMethod]
        public void Evaluate_NullBanlist_ThrowsArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => DeckLegalityEvaluator.Evaluate([], null!));
        }

        [TestMethod]
        public void Evaluate_NullCards_ThrowsArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => DeckLegalityEvaluator.Evaluate(null!, BuildBanlist()));
        }

        [TestMethod]
        public void Evaluate_SemiLimitedWithThreeCopies_ReportsViolation()
        {
            var card = BuildCard(1, "Reinforcement of the Army", konamiID: 300);
            var cards = new[] { BuildDeckCard(card, 1, quantity: 3) };
            var banlist = BuildBanlist((300, BanlistLimit.SemiLimited));

            var result = DeckLegalityEvaluator.Evaluate(cards, banlist);

            Assert.IsFalse(result.IsLegal);
        }

        [TestMethod]
        public void Evaluate_SemiLimitedWithTwoCopies_NoViolation()
        {
            var card = BuildCard(1, "Reinforcement of the Army", konamiID: 300);
            var cards = new[] { BuildDeckCard(card, 1, quantity: 2) };
            var banlist = BuildBanlist((300, BanlistLimit.SemiLimited));

            var result = DeckLegalityEvaluator.Evaluate(cards, banlist);

            Assert.IsTrue(result.IsLegal);
        }

        [TestMethod]
        public void Evaluate_TwoPasscodesShareName_GroupedTogetherByNameFallback()
        {
            // A "ghost duplicate" card (see PasscodeAliasResolver): the real card has a Konami ID; the
            // YGOProDeck-merge-only duplicate under a different CardID doesn't, so grouping falls back
            // to the shared name to combine both copies toward the same limit.
            var realCard = BuildCard(1, "Monster Reborn", konamiID: 400);
            var ghostCard = BuildCard(2, "Monster Reborn", konamiID: null);
            var cards = new[]
            {
                BuildDeckCard(realCard, 1, quantity: 1),
                BuildDeckCard(ghostCard, 2, quantity: 1)
            };
            var banlist = BuildBanlist((400, BanlistLimit.Forbidden));

            var result = DeckLegalityEvaluator.Evaluate(cards, banlist);

            Assert.IsFalse(result.IsLegal);
            Assert.AreEqual(2, result.Violations.Single().Copies);
            Assert.IsTrue(result.CardStatuses[1].IsViolation);
            Assert.IsTrue(result.CardStatuses[2].IsViolation);
        }

        [TestMethod]
        public void Evaluate_UnknownCard_Unrestricted()
        {
            var cards = new[] { BuildDeckCard(null, 999, quantity: 3) };
            var banlist = BuildBanlist((999, BanlistLimit.Forbidden));

            var result = DeckLegalityEvaluator.Evaluate(cards, banlist);

            Assert.IsTrue(result.IsLegal);
            Assert.AreEqual(0, result.CardStatuses.Count);
        }

        private static Banlist BuildBanlist(params (int KonamiID, BanlistLimit Limit)[] entries) =>
            new()
            {
                EffectiveDate = new DateOnly(2024, 1, 1),
                LimitsByKonamiID = entries.ToDictionary(e => e.KonamiID, e => e.Limit)
            };

        private static Card BuildCard(int id, string name, int? konamiID) =>
            new() { ID = id, Name = name, KonamiID = konamiID };

        private static DeckCardViewModel BuildDeckCard(Card? card, int cardID, int quantity) =>
            new() { Card = card, CardID = cardID, Quantity = quantity };
    }
}
