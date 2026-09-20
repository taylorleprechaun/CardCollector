using CardCollector.Data;
using CardCollector.Data.Models;
using CardCollector.Repository;
using CardCollector.Services;
using CardCollector.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;

namespace CardCollector.Tests.Services
{
    [TestClass]
    public sealed class MatchServiceTests
    {
        [TestMethod]
        public async Task AddAsync_Bye_StoresAWinWithNoGames()
        {
            using var context = InMemoryDbContextFactory.Create();
            var eventID = await AddEventAsync(context);
            var service = CreateService(context);
            var bye = BuildMatch("1", string.Empty);
            bye.GamesLost = 2;
            bye.IsBye = true;
            bye.Result = MatchResult.Loss;
            bye.WonDiceRoll = true;

            await service.AddAsync(eventID, bye);

            var saved = await context.Matches.SingleAsync();
            Assert.AreEqual(MatchResult.Win, saved.Result);
            Assert.AreEqual(0, saved.GamesLost);
            Assert.IsNull(saved.WonDiceRoll);
            Assert.AreEqual("Bye", saved.OpponentDeck);
        }

        [TestMethod]
        public async Task AddAsync_CallerSuppliesID_IgnoresItAndInserts()
        {
            using var context = InMemoryDbContextFactory.Create();
            var eventID = await AddEventAsync(context);
            var service = CreateService(context);
            var round = BuildMatch("1", "Test Opponent");
            round.ID = 42;

            var result = await service.AddAsync(eventID, round);

            Assert.IsTrue(result.Succeeded);
            Assert.AreNotEqual(42, (await context.Matches.SingleAsync()).ID);
        }

        [TestMethod]
        public async Task AddAsync_EventDoesNotExist_ReturnsNotFoundAndPersistsNothing()
        {
            using var context = InMemoryDbContextFactory.Create();
            var service = CreateService(context);

            var result = await service.AddAsync(999, BuildMatch("1", "Test Opponent"));

            Assert.IsFalse(result.Succeeded);
            Assert.IsTrue(result.NotFound);
            Assert.AreEqual(0, await context.Matches.CountAsync());
        }

        [TestMethod]
        public async Task AddAsync_InvalidRound_ReturnsErrorsAndPersistsNothing()
        {
            using var context = InMemoryDbContextFactory.Create();
            var eventID = await AddEventAsync(context);
            var service = CreateService(context);

            var result = await service.AddAsync(eventID, BuildMatch("1", "   "));

            Assert.IsFalse(result.Succeeded);
            Assert.IsFalse(result.NotFound);
            CollectionAssert.Contains(result.Errors.ToArray(), "Opponent deck is required.");
            Assert.AreEqual(0, await context.Matches.CountAsync());
        }

        [TestMethod]
        public async Task AddAsync_NullMatch_ThrowsArgumentNullException()
        {
            using var context = InMemoryDbContextFactory.Create();
            var service = CreateService(context);

            await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => service.AddAsync(1, null!));
        }

        [TestMethod]
        public async Task AddAsync_PaddedValues_PersistsTrimmedValuesAndReturnsTheSavedRound()
        {
            using var context = InMemoryDbContextFactory.Create();
            var eventID = await AddEventAsync(context);
            var service = CreateService(context);
            var padded = BuildMatch(" 1 ", "  Test Opponent  ");
            padded.Notes = "   ";

            var result = await service.AddAsync(eventID, padded);

            var saved = await context.Matches.SingleAsync();
            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(saved.ID, result.Match!.ID);
            Assert.AreEqual("Test Opponent", saved.OpponentDeck);
            Assert.AreEqual("1", saved.Round);
            Assert.IsNull(saved.Notes);
        }
        [TestMethod]
        public async Task AddAsync_RoundBelongsFirst_HasNoPreviousRound()
        {
            using var context = InMemoryDbContextFactory.Create();
            var eventID = await AddEventAsync(context);
            var service = CreateService(context);
            await service.AddAsync(eventID, BuildMatch("2", "Second"));

            var result = await service.AddAsync(eventID, BuildMatch("1", "First"));

            Assert.IsNull(result.PreviousMatchID);
            Assert.AreEqual("1", (await new MatchRepository(context).GetByEventAsync(eventID))[0].Round);
        }

        [TestMethod]
        public async Task AddAsync_RoundDeletedAndAddedBack_ReturnsToItsPlaceAfterTheRoundBeforeIt()
        {
            using var context = InMemoryDbContextFactory.Create();
            var eventID = await AddEventAsync(context);
            var service = CreateService(context);
            foreach (var label in new[] { "1", "2", "3", "4", "5" })
                await service.AddAsync(eventID, BuildMatch(label, $"Opponent {label}"));
            var third = await context.Matches.SingleAsync(m => m.Round == "3");
            var second = await context.Matches.SingleAsync(m => m.Round == "2");
            await service.DeleteAsync(eventID, third.ID);

            var result = await service.AddAsync(eventID, BuildMatch("3", "Opponent 3"));

            var rounds = await new MatchRepository(context).GetByEventAsync(eventID);
            CollectionAssert.AreEqual(new[] { "1", "2", "3", "4", "5" }, rounds.Select(m => m.Round).ToArray());
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5 }, rounds.Select(m => m.Sequence).ToArray());
            Assert.AreEqual(second.ID, result.PreviousMatchID);
        }
        [TestMethod]
        public async Task AddAsync_TopCutRound_GoesAfterTheNumberedRounds()
        {
            using var context = InMemoryDbContextFactory.Create();
            var eventID = await AddEventAsync(context);
            var service = CreateService(context);
            var last = await service.AddAsync(eventID, BuildMatch("2", "Second"));
            await service.AddAsync(eventID, BuildMatch("1", "First"));

            var result = await service.AddAsync(eventID, BuildMatch("Top 8", "Cut"));

            Assert.AreEqual(last.Match!.ID, result.PreviousMatchID);
            Assert.AreEqual("Top 8", (await new MatchRepository(context).GetByEventAsync(eventID))[2].Round);
        }
        [TestMethod]
        public async Task DeleteAsync_MiddleRound_ResequencesAndUpdatesTheSummary()
        {
            using var context = InMemoryDbContextFactory.Create();
            var eventID = await AddEventAsync(context);
            var service = CreateService(context);
            await service.AddAsync(eventID, BuildMatch("1", "First"));
            var middle = await service.AddAsync(eventID, BuildMatch("2", "Second"));
            await service.AddAsync(eventID, BuildMatch("3", "Third"));

            var deleted = await service.DeleteAsync(eventID, middle.Match!.ID);

            var summary = await service.GetSummaryAsync(eventID);
            Assert.IsTrue(deleted);
            Assert.AreEqual(2, summary.RoundCount);
            CollectionAssert.AreEqual(new[] { 1, 2 }, await context.Matches.OrderBy(m => m.Sequence).Select(m => m.Sequence).ToArrayAsync());
        }

        [TestMethod]
        public async Task GetOpponentDecksAsync_RoundsExist_ReturnsMostPlayedFirst()
        {
            using var context = InMemoryDbContextFactory.Create();
            var eventID = await AddEventAsync(context);
            var service = CreateService(context);
            await service.AddAsync(eventID, BuildMatch("1", "Once"));
            await service.AddAsync(eventID, BuildMatch("2", "Twice"));
            await service.AddAsync(eventID, BuildMatch("3", "Twice"));

            var opponents = await service.GetOpponentDecksAsync();

            CollectionAssert.AreEqual(new[] { "Twice", "Once" }, opponents.ToArray());
        }

        [TestMethod]
        public async Task GetSummaryAsync_RoundsExist_ReflectsTheStoredResultsNotTheScores()
        {
            using var context = InMemoryDbContextFactory.Create();
            var eventID = await AddEventAsync(context);
            var service = CreateService(context);
            var overridden = BuildMatch("1", "Test Opponent");
            overridden.GamesLost = 2;
            overridden.GamesWon = 1;
            overridden.Result = MatchResult.Win;
            await service.AddAsync(eventID, overridden);

            var summary = await service.GetSummaryAsync(eventID);

            Assert.AreEqual("1-0-0", summary.MatchRecord.ToString());
            Assert.AreEqual("1-2-0", summary.GameRecord.ToString());
            Assert.AreEqual("2", summary.NextRound);
        }

        [TestMethod]
        public async Task UpdateAsync_ByeEditedIntoRegularRoundAndBack_AppliesTheByeRulesOnlyToTheBye()
        {
            using var context = InMemoryDbContextFactory.Create();
            var eventID = await AddEventAsync(context);
            var service = CreateService(context);
            var bye = BuildMatch("1", "Bye");
            bye.IsBye = true;
            var added = await service.AddAsync(eventID, bye);

            var regular = BuildMatch("1", "Test Opponent");
            regular.GamesLost = 2;
            regular.ID = added.Match!.ID;
            regular.Result = MatchResult.Loss;
            await service.UpdateAsync(eventID, regular);
            var afterRegular = await context.Matches.AsNoTracking().SingleAsync();

            var backToBye = BuildMatch("1", "Bye");
            backToBye.GamesLost = 2;
            backToBye.ID = added.Match.ID;
            backToBye.IsBye = true;
            backToBye.Result = MatchResult.Loss;
            await service.UpdateAsync(eventID, backToBye);
            var afterBye = await context.Matches.AsNoTracking().SingleAsync();

            Assert.AreEqual(MatchResult.Loss, afterRegular.Result);
            Assert.AreEqual(2, afterRegular.GamesLost);
            Assert.AreEqual(MatchResult.Win, afterBye.Result);
            Assert.AreEqual(0, afterBye.GamesLost);
        }

        [TestMethod]
        public async Task SortAsync_RoundsOutOfOrder_PutsThemInRoundOrder()
        {
            using var context = InMemoryDbContextFactory.Create();
            var eventID = await AddEventAsync(context);
            context.Matches.AddRange(
                new Match { EventID = eventID, OpponentDeck = "Second", Round = "2", Sequence = 1 },
                new Match { EventID = eventID, OpponentDeck = "First", Round = "1", Sequence = 2 },
                new Match { EventID = eventID, OpponentDeck = "Third", Round = "3", Sequence = 3 });
            await context.SaveChangesAsync();
            var service = CreateService(context);

            var sorted = await service.SortAsync(eventID);

            var rounds = await new MatchRepository(context).GetByEventAsync(eventID);
            Assert.IsTrue(sorted);
            CollectionAssert.AreEqual(new[] { "1", "2", "3" }, rounds.Select(m => m.Round).ToArray());
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, rounds.Select(m => m.Sequence).ToArray());
        }

        [TestMethod]
        public async Task SortAsync_TopCutRounds_GoAfterTheNumberedRounds()
        {
            using var context = InMemoryDbContextFactory.Create();
            var eventID = await AddEventAsync(context);
            context.Matches.AddRange(
                new Match { EventID = eventID, OpponentDeck = "Finals", Round = "Finals", Sequence = 1 },
                new Match { EventID = eventID, OpponentDeck = "Round 2", Round = "2", Sequence = 2 },
                new Match { EventID = eventID, OpponentDeck = "Top 8", Round = "Top 8", Sequence = 3 },
                new Match { EventID = eventID, OpponentDeck = "Round 1", Round = "1", Sequence = 4 });
            await context.SaveChangesAsync();
            var service = CreateService(context);

            await service.SortAsync(eventID);

            var rounds = await new MatchRepository(context).GetByEventAsync(eventID);
            CollectionAssert.AreEqual(new[] { "1", "2", "Top 8", "Finals" }, rounds.Select(m => m.Round).ToArray());
        }

        [TestMethod]
        public async Task SortAsync_AlreadyInOrder_ReturnsFalse()
        {
            using var context = InMemoryDbContextFactory.Create();
            var eventID = await AddEventAsync(context);
            var service = CreateService(context);
            await service.AddAsync(eventID, BuildMatch("1", "First"));
            await service.AddAsync(eventID, BuildMatch("2", "Second"));

            var sorted = await service.SortAsync(eventID);

            Assert.IsFalse(sorted);
        }

        [TestMethod]
        public async Task SortAsync_OtherEventsRounds_AreNotTouched()
        {
            using var context = InMemoryDbContextFactory.Create();
            var eventID = await AddEventAsync(context);
            var otherEventID = await AddEventAsync(context);
            context.Matches.AddRange(
                new Match { EventID = eventID, OpponentDeck = "Second", Round = "2", Sequence = 1 },
                new Match { EventID = eventID, OpponentDeck = "First", Round = "1", Sequence = 2 },
                new Match { EventID = otherEventID, OpponentDeck = "Out of order elsewhere", Round = "5", Sequence = 1 },
                new Match { EventID = otherEventID, OpponentDeck = "Also elsewhere", Round = "4", Sequence = 2 });
            await context.SaveChangesAsync();
            var service = CreateService(context);

            await service.SortAsync(eventID);

            var untouched = await new MatchRepository(context).GetByEventAsync(otherEventID);
            CollectionAssert.AreEqual(new[] { "5", "4" }, untouched.Select(m => m.Round).ToArray());
        }

        [TestMethod]
        public async Task SortAsync_EventWithNoRounds_ReturnsFalse()
        {
            using var context = InMemoryDbContextFactory.Create();
            var service = CreateService(context);

            var sorted = await service.SortAsync(999);

            Assert.IsFalse(sorted);
        }

        [TestMethod]
        public async Task UpdateAsync_EventNoLongerHasTheRound_ReturnsNotFound()
        {
            using var context = InMemoryDbContextFactory.Create();
            var eventID = await AddEventAsync(context);
            var service = CreateService(context);
            var missing = BuildMatch("1", "Test Opponent");
            missing.ID = 999;

            var result = await service.UpdateAsync(eventID, missing);

            Assert.IsFalse(result.Succeeded);
            Assert.IsTrue(result.NotFound);
        }

        [TestMethod]
        public async Task UpdateAsync_InvalidRound_ReturnsErrorsAndChangesNothing()
        {
            using var context = InMemoryDbContextFactory.Create();
            var eventID = await AddEventAsync(context);
            var service = CreateService(context);
            var added = await service.AddAsync(eventID, BuildMatch("1", "Test Opponent"));
            var invalid = BuildMatch("1", "Changed");
            invalid.GamesWon = 9;
            invalid.ID = added.Match!.ID;

            var result = await service.UpdateAsync(eventID, invalid);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual("Test Opponent", (await context.Matches.SingleAsync()).OpponentDeck);
        }

        [TestMethod]
        public async Task UpdateAsync_NullMatch_ThrowsArgumentNullException()
        {
            using var context = InMemoryDbContextFactory.Create();
            var service = CreateService(context);

            await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => service.UpdateAsync(1, null!));
        }

        [TestMethod]
        public async Task UpdateAsync_ScoreEditedWithOverriddenResult_KeepsTheSubmittedResult()
        {
            using var context = InMemoryDbContextFactory.Create();
            var eventID = await AddEventAsync(context);
            var service = CreateService(context);
            var added = await service.AddAsync(eventID, BuildMatch("1", "Test Opponent"));
            var edited = BuildMatch("1", "Test Opponent");
            edited.GamesLost = 2;
            edited.GamesWon = 1;
            edited.ID = added.Match!.ID;
            edited.Result = MatchResult.Win;

            await service.UpdateAsync(eventID, edited);

            var saved = await context.Matches.SingleAsync();
            Assert.AreEqual(MatchResult.Win, saved.Result);
            Assert.AreEqual(2, saved.GamesLost);
        }
        private static async Task<int> AddEventAsync(AppDBContext context)
        {
            var tournamentEvent = new Event { Date = new DateOnly(2024, 5, 4), DeckName = "Sample Deck", Location = "Test Hobby Shop" };
            context.Events.Add(tournamentEvent);
            await context.SaveChangesAsync();
            return tournamentEvent.ID;
        }

        private static Match BuildMatch(string round, string opponent) =>
            new()
            {
                OpponentDeck = opponent,
                Result = MatchResult.Tie,
                Round = round
            };

        private static MatchService CreateService(AppDBContext context) =>
            new(new EventRepository(context), new MatchRepository(context));
    }
}
