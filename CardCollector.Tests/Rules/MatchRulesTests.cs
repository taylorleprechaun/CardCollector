using CardCollector.Data.Models;
using CardCollector.Rules;

namespace CardCollector.Tests.Rules
{
    [TestClass]
    public sealed class MatchRulesTests
    {
        [TestMethod]
        [DataRow(new string[0], "1", 0, DisplayName = "First round of an empty event")]
        [DataRow(new[] { "1", "2", "3" }, "4", 3, DisplayName = "Next round goes last")]
        [DataRow(new[] { "1", "2", "4", "5" }, "3", 2, DisplayName = "Re-added middle round goes between its neighbours")]
        [DataRow(new[] { "2", "3" }, "1", 0, DisplayName = "Earlier round goes first")]
        [DataRow(new[] { "1", "2", "3" }, "3", 3, DisplayName = "Duplicate label goes after the existing one")]
        [DataRow(new[] { "1", "2", "3" }, "Top 8", 3, DisplayName = "Top cut goes after the numbered rounds")]
        [DataRow(new[] { "1", "2", "Top 4" }, "Top 8", 2, DisplayName = "Top 8 goes before Top 4")]
        [DataRow(new[] { "1", "Top 8", "Top 4" }, "Finals", 3, DisplayName = "Finals goes last")]
        [DataRow(new[] { "1", "Top 8" }, "2", 1, DisplayName = "Numbered round goes before the top cut")]
        [DataRow(new[] { "1", "2", "Top 8" }, "top 4", 3, DisplayName = "Top-cut label is case-insensitive")]
        [DataRow(new[] { "1", "2", "4" }, "Semifinal", 3, DisplayName = "Unrecognised label goes last")]
        [DataRow(new[] { "1", "Semifinal", "3" }, "2", 1, DisplayName = "Unrecognised existing labels are skipped")]
        [DataRow(new[] { "1", "2" }, "  ", 2, DisplayName = "Blank label goes last")]
        public void GetInsertPosition_ExistingRounds_ReturnsZeroBasedIndex(string[] existing, string round, int expected)
        {
            var position = MatchRules.GetInsertPosition(existing, round);

            Assert.AreEqual(expected, position);
        }

        [TestMethod]
        public void GetInsertPosition_NullRounds_Throws()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => MatchRules.GetInsertPosition(null!, "1"));
        }

        [TestMethod]
        [DataRow(new string[0], "1", DisplayName = "No rounds")]
        [DataRow(new[] { "1" }, "2", DisplayName = "After round 1")]
        [DataRow(new[] { "1", "2", "3", "4", "5" }, "6", DisplayName = "After round 5")]
        [DataRow(new[] { "1", "2", "3", "4", "5", "Top 8" }, "Top 4", DisplayName = "After Top 8")]
        [DataRow(new[] { "5", "Top 8", "Top 4" }, "Finals", DisplayName = "After Top 4")]
        [DataRow(new[] { "5", "top 8" }, "Top 4", DisplayName = "Top-cut label is case-insensitive")]
        [DataRow(new[] { "5", "Top 8", "Top 4", "Finals" }, "", DisplayName = "After Finals")]
        [DataRow(new[] { "1", "Semifinal" }, "", DisplayName = "Unrecognised label")]
        [DataRow(new[] { "1", "  " }, "1", DisplayName = "Blank last label")]
        public void NextRoundLabel_ExistingRounds_ReturnsSuggestion(string[] rounds, string expected)
        {
            var label = MatchRules.NextRoundLabel(rounds);

            Assert.AreEqual(expected, label);
        }

        [TestMethod]
        public void NextRoundLabel_NullRounds_Throws()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => MatchRules.NextRoundLabel(null!));
        }

        [TestMethod]
        public void Normalize_Bye_ForcesWinWithNoGamesOrDiceRoll()
        {
            var bye = new Match
            {
                GamesLost = 2,
                GamesTied = 1,
                GamesWon = 1,
                IsBye = true,
                OpponentDeck = "Test Opponent",
                Result = MatchResult.Loss,
                Round = "3",
                WonDiceRoll = true
            };

            var normalized = MatchRules.Normalize(bye);

            Assert.AreEqual(0, normalized.GamesWon + normalized.GamesLost + normalized.GamesTied);
            Assert.AreEqual(MatchResult.Win, normalized.Result);
            Assert.IsNull(normalized.WonDiceRoll);
            Assert.AreEqual("Test Opponent", normalized.OpponentDeck);
        }

        [TestMethod]
        public void Normalize_ByeWithBlankOpponent_UsesByeName()
        {
            var bye = new Match { IsBye = true, OpponentDeck = "   ", Round = "1" };

            var normalized = MatchRules.Normalize(bye);

            Assert.AreEqual(MatchRules.BYE_OPPONENT_NAME, normalized.OpponentDeck);
        }

        [TestMethod]
        public void Normalize_NonBye_KeepsScoreResultAndDiceRoll()
        {
            var round = new Match
            {
                GamesLost = 1,
                GamesWon = 2,
                OpponentDeck = "Test Opponent",
                Result = MatchResult.Loss,
                Round = "2",
                WonDiceRoll = false
            };

            var normalized = MatchRules.Normalize(round);

            Assert.AreEqual(2, normalized.GamesWon);
            Assert.AreEqual(1, normalized.GamesLost);
            Assert.AreEqual(MatchResult.Loss, normalized.Result);
            Assert.AreEqual(false, normalized.WonDiceRoll);
        }

        [TestMethod]
        public void Normalize_NullMatch_Throws()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => MatchRules.Normalize(null!));
        }

        [TestMethod]
        public void Normalize_TextFields_AreTrimmedAndBlankNotesBecomeNull()
        {
            var round = new Match { Notes = "   ", OpponentDeck = "  Test Opponent  ", Round = " 4 " };

            var normalized = MatchRules.Normalize(round);

            Assert.AreEqual("Test Opponent", normalized.OpponentDeck);
            Assert.AreEqual("4", normalized.Round);
            Assert.IsNull(normalized.Notes);
        }
        [TestMethod]
        [DataRow(2, 0, 0, MatchResult.Win, DisplayName = "2-0")]
        [DataRow(2, 1, 0, MatchResult.Win, DisplayName = "2-1")]
        [DataRow(1, 2, 0, MatchResult.Loss, DisplayName = "1-2")]
        [DataRow(0, 2, 0, MatchResult.Loss, DisplayName = "0-2")]
        [DataRow(1, 1, 0, MatchResult.Tie, DisplayName = "1-1-0")]
        [DataRow(1, 1, 1, MatchResult.Tie, DisplayName = "1-1-1")]
        [DataRow(0, 0, 0, MatchResult.Tie, DisplayName = "0-0-0")]
        [DataRow(1, 0, 0, MatchResult.Win, DisplayName = "1-0-0")]
        public void SuggestResult_Score_ReturnsExpectedResult(int won, int lost, int tied, MatchResult expected)
        {
            var result = MatchRules.SuggestResult(won, lost, tied);

            Assert.AreEqual(expected, result);
        }
        [TestMethod]
        [DataRow(new string[0], true, DisplayName = "No rounds")]
        [DataRow(new[] { "1" }, true, DisplayName = "One round")]
        [DataRow(new[] { "1", "2", "3" }, true, DisplayName = "Numbered rounds in order")]
        [DataRow(new[] { "2", "1", "3" }, false, DisplayName = "Numbered rounds out of order")]
        [DataRow(new[] { "1", "1", "2" }, true, DisplayName = "Repeated label")]
        [DataRow(new[] { "1", "2", "Top 8", "Top 4", "Finals" }, true, DisplayName = "Top cut after the numbered rounds")]
        [DataRow(new[] { "1", "Top 4", "Top 8" }, false, DisplayName = "Top cut out of order")]
        [DataRow(new[] { "Top 8", "1" }, false, DisplayName = "Top cut before a numbered round")]
        [DataRow(new[] { "1", "Semifinal" }, true, DisplayName = "Unrecognised label last")]
        [DataRow(new[] { "1", "Semifinal", "2" }, false, DisplayName = "Unrecognised label in the middle")]
        [DataRow(new[] { "1", "top 8" }, true, DisplayName = "Top-cut label is case-insensitive")]
        public void IsInRoundOrder_Rounds_ReturnsWhetherTheyAreInOrder(string[] rounds, bool expected)
        {
            var inOrder = MatchRules.IsInRoundOrder(rounds);

            Assert.AreEqual(expected, inOrder);
        }

        [TestMethod]
        public void IsInRoundOrder_NullRounds_Throws()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => MatchRules.IsInRoundOrder(null!));
        }

        [TestMethod]
        public void OrderByRound_OutOfOrderRounds_ReturnsThemInRoundOrder()
        {
            var rounds = new[]
            {
                new Match { ID = 1, Round = "Finals" },
                new Match { ID = 2, Round = "3" },
                new Match { ID = 3, Round = "Top 8" },
                new Match { ID = 4, Round = "1" },
                new Match { ID = 5, Round = "Semifinal" },
                new Match { ID = 6, Round = "2" }
            };

            var ordered = MatchRules.OrderByRound(rounds);

            CollectionAssert.AreEqual(new[] { 4, 6, 2, 3, 1, 5 }, ordered.Select(m => m.ID).ToArray());
        }

        [TestMethod]
        public void OrderByRound_SameLabel_KeepsTheirExistingOrder()
        {
            var rounds = new[]
            {
                new Match { ID = 1, Round = "2" },
                new Match { ID = 2, Round = "1" },
                new Match { ID = 3, Round = "2" }
            };

            var ordered = MatchRules.OrderByRound(rounds);

            CollectionAssert.AreEqual(new[] { 2, 1, 3 }, ordered.Select(m => m.ID).ToArray());
        }

        [TestMethod]
        public void OrderByRound_NullMatches_Throws()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => MatchRules.OrderByRound(null!));
        }

        [TestMethod]
        public void Summarize_MixedRounds_TalliesAndSuggestsNextRound()
        {
            var rounds = new[]
            {
                new Match { GamesWon = 2, Result = MatchResult.Win, Round = "1", WonDiceRoll = true },
                new Match { GamesLost = 2, Result = MatchResult.Loss, Round = "2", WonDiceRoll = false },
                new Match { IsBye = true, Result = MatchResult.Win, Round = "3" }
            };

            var summary = MatchRules.Summarize(rounds);

            Assert.AreEqual("2-1-0", summary.MatchRecord.ToString());
            Assert.AreEqual("2-2-0", summary.GameRecord.ToString());
            Assert.AreEqual("1-1", summary.DiceRecord.ToString());
            Assert.AreEqual("4", summary.NextRound);
            Assert.AreEqual(3, summary.RoundCount);
        }

        [TestMethod]
        public void Summarize_NoRounds_ReturnsZeroesAndFirstRound()
        {
            var summary = MatchRules.Summarize([]);

            Assert.AreEqual(0, summary.RoundCount);
            Assert.AreEqual("1", summary.NextRound);
            Assert.AreEqual(0, summary.MatchRecord.Total);
        }

        [TestMethod]
        public void Validate_ByeWithoutOpponent_IsValid()
        {
            var bye = new Match { IsBye = true, OpponentDeck = string.Empty, Result = MatchResult.Win, Round = "1" };

            var errors = MatchRules.Validate(bye);

            Assert.AreEqual(0, errors.Count);
        }

        [TestMethod]
        [DataRow(-1, 0, 0, DisplayName = "Negative won")]
        [DataRow(3, 0, 0, DisplayName = "Won over two")]
        [DataRow(0, 3, 0, DisplayName = "Lost over two")]
        [DataRow(0, -1, 0, DisplayName = "Negative lost")]
        [DataRow(0, 0, -1, DisplayName = "Negative tied")]
        public void Validate_GamesOutOfRange_ReportsGamesError(int won, int lost, int tied)
        {
            var round = new Match { GamesLost = lost, GamesTied = tied, GamesWon = won, OpponentDeck = "Test Opponent", Round = "1" };

            var errors = MatchRules.Validate(round);

            Assert.IsTrue(errors.Any(e => e.StartsWith("Games ", StringComparison.Ordinal)));
        }

        [TestMethod]
        [DataRow("", DisplayName = "Empty round")]
        [DataRow("   ", DisplayName = "Whitespace round")]
        [DataRow("123456789012345678901", DisplayName = "Round over the limit")]
        public void Validate_InvalidRound_ReportsRoundError(string roundLabel)
        {
            var round = new Match { OpponentDeck = "Test Opponent", Round = roundLabel };

            var errors = MatchRules.Validate(round);

            Assert.IsTrue(errors.Any(e => e.StartsWith("Round ", StringComparison.Ordinal)));
        }

        [TestMethod]
        public void Validate_NonByeWithoutOpponent_ReportsOpponentError()
        {
            var round = new Match { OpponentDeck = string.Empty, Round = "1" };

            var errors = MatchRules.Validate(round);

            CollectionAssert.Contains(errors.ToList(), "Opponent deck is required.");
        }

        [TestMethod]
        public void Validate_NullMatch_Throws()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => MatchRules.Validate(null!));
        }

        [TestMethod]
        public void Validate_OpponentOverTheLimit_ReportsOpponentError()
        {
            var round = new Match { OpponentDeck = new string('x', MatchRules.MAX_OPPONENT_LENGTH + 1), Round = "1" };

            var errors = MatchRules.Validate(round);

            Assert.IsTrue(errors.Any(e => e.StartsWith("Opponent deck must be", StringComparison.Ordinal)));
        }

        [TestMethod]
        public void Validate_TwoWinsTwoLossesAndManyTies_IsAllowed()
        {
            var round = new Match { GamesLost = 2, GamesTied = 7, GamesWon = 2, OpponentDeck = "Test Opponent", Round = "1" };

            var errors = MatchRules.Validate(round);

            Assert.AreEqual(0, errors.Count);
        }

        [TestMethod]
        public void Validate_UndefinedResult_ReportsResultError()
        {
            var round = new Match { OpponentDeck = "Test Opponent", Result = (MatchResult)99, Round = "1" };

            var errors = MatchRules.Validate(round);

            CollectionAssert.Contains(errors.ToList(), "Result is not valid.");
        }

        [TestMethod]
        public void Validate_ValidRound_HasNoErrors()
        {
            var round = new Match { GamesLost = 1, GamesWon = 2, OpponentDeck = "Test Opponent", Result = MatchResult.Win, Round = "1" };

            var errors = MatchRules.Validate(round);

            Assert.AreEqual(0, errors.Count);
        }
    }
}
