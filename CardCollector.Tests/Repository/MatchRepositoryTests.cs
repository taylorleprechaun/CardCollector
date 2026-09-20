using CardCollector.Data.Models;
using CardCollector.Repository;
using CardCollector.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;

namespace CardCollector.Tests.Repository
{
    [TestClass]
    public sealed class MatchRepositoryTests
    {
        [TestMethod]
        public async Task AddAsync_EmptyEvent_StartsAtSequenceOne()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new MatchRepository(context);

            var id = await repository.AddAsync(1, BuildMatch("1", "Test Opponent"));

            var saved = await repository.GetAsync(1, id);
            Assert.AreEqual(1, saved!.Sequence);
        }

        [TestMethod]
        public async Task AddAsync_ExistingRounds_AppendsAfterTheLastSequence()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new MatchRepository(context);
            await repository.AddAsync(1, BuildMatch("1", "First"));
            await repository.AddAsync(1, BuildMatch("2", "Second"));

            var id = await repository.AddAsync(1, BuildMatch("3", "Third"));

            var saved = await repository.GetAsync(1, id);
            Assert.AreEqual(3, saved!.Sequence);
        }

        [TestMethod]
        public async Task AddAsync_ExistingSequenceHasGaps_LeavesTheRoundsContiguous()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new MatchRepository(context);
            context.Matches.AddRange(
                new Match { EventID = 1, OpponentDeck = "First", Round = "1", Sequence = 1 },
                new Match { EventID = 1, OpponentDeck = "Second", Round = "2", Sequence = 5 });
            await context.SaveChangesAsync();

            await repository.AddAsync(1, BuildMatch("3", "Third"));

            var rounds = await repository.GetByEventAsync(1);
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, rounds.Select(m => m.Sequence).ToArray());
            Assert.AreEqual("Third", rounds[2].OpponentDeck);
        }

        [TestMethod]
        public async Task AddAsync_NewRound_PersistsFieldsAndAuditDates()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new MatchRepository(context);
            var source = BuildMatch("Top 8", "Test Opponent");
            source.GamesLost = 1;
            source.GamesWon = 2;
            source.Notes = "line one\nline two";
            source.Result = MatchResult.Win;
            source.WonDiceRoll = true;

            var id = await repository.AddAsync(1, source);

            var saved = await repository.GetAsync(1, id);
            Assert.AreEqual("Top 8", saved!.Round);
            Assert.AreEqual("Test Opponent", saved.OpponentDeck);
            Assert.AreEqual(2, saved.GamesWon);
            Assert.AreEqual(1, saved.GamesLost);
            Assert.AreEqual("line one\nline two", saved.Notes);
            Assert.AreEqual(MatchResult.Win, saved.Result);
            Assert.AreEqual(true, saved.WonDiceRoll);
            Assert.AreNotEqual(default, saved.DateCreated);
            Assert.AreEqual(saved.DateCreated, saved.DateModified);
        }

        [TestMethod]
        public async Task AddAsync_NullMatch_Throws()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new MatchRepository(context);

            await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => repository.AddAsync(1, null!));
        }

        [TestMethod]
        public async Task AddAsync_PositionInAnotherEvent_DoesNotMoveThatEventsRounds()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new MatchRepository(context);
            await repository.AddAsync(2, BuildMatch("1", "Other A"));
            await repository.AddAsync(2, BuildMatch("2", "Other B"));

            await repository.AddAsync(1, BuildMatch("1", "This event"), position: 0);

            CollectionAssert.AreEqual(new[] { 1, 2 }, (await repository.GetByEventAsync(2)).Select(m => m.Sequence).ToArray());
        }

        [TestMethod]
        public async Task AddAsync_PositionInTheMiddle_InsertsThereAndMovesTheLaterRoundsDown()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new MatchRepository(context);
            await repository.AddAsync(1, BuildMatch("1", "First"));
            await repository.AddAsync(1, BuildMatch("2", "Second"));
            await repository.AddAsync(1, BuildMatch("4", "Fourth"));

            var id = await repository.AddAsync(1, BuildMatch("3", "Third"), position: 2);

            var rounds = await repository.GetByEventAsync(1);
            CollectionAssert.AreEqual(new[] { "First", "Second", "Third", "Fourth" }, rounds.Select(m => m.OpponentDeck).ToArray());
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, rounds.Select(m => m.Sequence).ToArray());
            Assert.AreEqual(id, rounds[2].ID);
        }

        [TestMethod]
        public async Task AddAsync_PositionOutOfRange_IsClampedToTheEnds()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new MatchRepository(context);
            await repository.AddAsync(1, BuildMatch("2", "Second"));

            await repository.AddAsync(1, BuildMatch("3", "Last"), position: 99);
            await repository.AddAsync(1, BuildMatch("1", "First"), position: -5);

            var rounds = await repository.GetByEventAsync(1);
            CollectionAssert.AreEqual(new[] { "First", "Second", "Last" }, rounds.Select(m => m.OpponentDeck).ToArray());
        }

        [TestMethod]
        public async Task AddAsync_PositionZero_InsertsFirst()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new MatchRepository(context);
            await repository.AddAsync(1, BuildMatch("2", "Second"));

            await repository.AddAsync(1, BuildMatch("1", "First"), position: 0);

            var rounds = await repository.GetByEventAsync(1);
            CollectionAssert.AreEqual(new[] { "First", "Second" }, rounds.Select(m => m.OpponentDeck).ToArray());
        }
        [TestMethod]
        public async Task AddAsync_RoundInAnotherEvent_DoesNotAffectSequencing()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new MatchRepository(context);
            await repository.AddAsync(2, BuildMatch("1", "Other event"));

            var id = await repository.AddAsync(1, BuildMatch("1", "This event"));

            var saved = await repository.GetAsync(1, id);
            Assert.AreEqual(1, saved!.Sequence);
        }
        [TestMethod]
        public async Task DeleteAsync_LastRound_LeavesTheOthersUnchanged()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new MatchRepository(context);
            await repository.AddAsync(1, BuildMatch("1", "First"));
            await repository.AddAsync(1, BuildMatch("2", "Second"));
            var lastId = await repository.AddAsync(1, BuildMatch("3", "Third"));

            var deleted = await repository.DeleteAsync(1, lastId);

            Assert.IsTrue(deleted);
            CollectionAssert.AreEqual(new[] { 1, 2 }, (await repository.GetByEventAsync(1)).Select(m => m.Sequence).ToArray());
        }

        [TestMethod]
        public async Task DeleteAsync_MiddleRound_ResequencesTheRestContiguously()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new MatchRepository(context);
            await repository.AddAsync(1, BuildMatch("1", "First"));
            var middleId = await repository.AddAsync(1, BuildMatch("2", "Second"));
            await repository.AddAsync(1, BuildMatch("3", "Third"));
            await repository.AddAsync(1, BuildMatch("4", "Fourth"));

            var deleted = await repository.DeleteAsync(1, middleId);

            var remaining = await repository.GetByEventAsync(1);
            Assert.IsTrue(deleted);
            CollectionAssert.AreEqual(new[] { "First", "Third", "Fourth" }, remaining.Select(m => m.OpponentDeck).ToArray());
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, remaining.Select(m => m.Sequence).ToArray());
        }

        [TestMethod]
        public async Task DeleteAsync_OnlyRound_LeavesTheEventWithNoRounds()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new MatchRepository(context);
            var id = await repository.AddAsync(1, BuildMatch("1", "First"));

            await repository.DeleteAsync(1, id);

            Assert.AreEqual(0, (await repository.GetByEventAsync(1)).Count);
        }

        [TestMethod]
        public async Task DeleteAsync_OtherEventsRounds_AreNotRenumbered()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new MatchRepository(context);
            var id = await repository.AddAsync(1, BuildMatch("1", "First"));
            await repository.AddAsync(2, BuildMatch("1", "Other A"));
            await repository.AddAsync(2, BuildMatch("2", "Other B"));

            await repository.DeleteAsync(1, id);

            CollectionAssert.AreEqual(new[] { 1, 2 }, (await repository.GetByEventAsync(2)).Select(m => m.Sequence).ToArray());
        }

        [TestMethod]
        public async Task DeleteAsync_RoundBelongsToAnotherEvent_ReturnsFalseAndKeepsIt()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new MatchRepository(context);
            var id = await repository.AddAsync(1, BuildMatch("1", "First"));

            var deleted = await repository.DeleteAsync(2, id);

            Assert.IsFalse(deleted);
            Assert.AreEqual(1, await context.Matches.CountAsync());
        }

        [TestMethod]
        public async Task DeleteAsync_UnknownRound_ReturnsFalse()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new MatchRepository(context);

            var deleted = await repository.DeleteAsync(1, 999);

            Assert.IsFalse(deleted);
        }

        [TestMethod]
        public async Task GetAsync_RoundBelongsToAnotherEvent_ReturnsNull()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new MatchRepository(context);
            var id = await repository.AddAsync(1, BuildMatch("1", "First"));

            var found = await repository.GetAsync(2, id);

            Assert.IsNull(found);
        }

        [TestMethod]
        public async Task GetByEventAsync_RoundsAddedOutOfOrder_ReturnsPlayOrder()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new MatchRepository(context);
            context.Matches.AddRange(
                new Match { EventID = 1, OpponentDeck = "Second", Round = "2", Sequence = 2 },
                new Match { EventID = 1, OpponentDeck = "First", Round = "1", Sequence = 1 },
                new Match { EventID = 2, OpponentDeck = "Other event", Round = "1", Sequence = 1 });
            await context.SaveChangesAsync();

            var rounds = await repository.GetByEventAsync(1);

            CollectionAssert.AreEqual(new[] { "First", "Second" }, rounds.Select(m => m.OpponentDeck).ToArray());
        }

        [TestMethod]
        public async Task GetOpponentDecksAsync_ByeRounds_AreExcluded()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new MatchRepository(context);
            var bye = BuildMatch("1", "BYE");
            bye.IsBye = true;
            await repository.AddAsync(1, bye);
            await repository.AddAsync(1, BuildMatch("2", "Test Opponent"));

            var opponents = await repository.GetOpponentDecksAsync();

            CollectionAssert.AreEqual(new[] { "Test Opponent" }, opponents.ToArray());
        }

        [TestMethod]
        public async Task GetOpponentDecksAsync_ManyRounds_OrdersByFrequencyThenName()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new MatchRepository(context);
            await AddRoundsAsync(repository, "Rare", 1);
            await AddRoundsAsync(repository, "Common", 3);
            await AddRoundsAsync(repository, "Alpha", 2);
            await AddRoundsAsync(repository, "Beta", 2);

            var opponents = await repository.GetOpponentDecksAsync();

            CollectionAssert.AreEqual(new[] { "Common", "Alpha", "Beta", "Rare" }, opponents.ToArray());
        }
        [TestMethod]
        public async Task GetOpponentDecksAsync_MoreThanTwoHundredDecks_ReturnsTwoHundred()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new MatchRepository(context);
            context.Matches.AddRange(Enumerable.Range(1, 205).Select(i => new Match { EventID = 1, OpponentDeck = $"Deck {i:D3}", Round = "1", Sequence = i }));
            await context.SaveChangesAsync();

            var opponents = await repository.GetOpponentDecksAsync();

            Assert.AreEqual(200, opponents.Count);
        }

        [TestMethod]
        public async Task UpdateAsync_ByeChangedToRegularRoundAndBack_PersistsEachChange()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new MatchRepository(context);
            var bye = BuildMatch("1", "Bye");
            bye.IsBye = true;
            bye.Result = MatchResult.Win;
            var id = await repository.AddAsync(1, bye);

            await repository.UpdateAsync(1, new Match { GamesLost = 1, GamesWon = 2, ID = id, IsBye = false, OpponentDeck = "Test Opponent", Result = MatchResult.Win, Round = "1", WonDiceRoll = true });
            var asRegular = await repository.GetAsync(1, id);
            await repository.UpdateAsync(1, new Match { ID = id, IsBye = true, OpponentDeck = "Bye", Result = MatchResult.Win, Round = "1" });
            var asBye = await repository.GetAsync(1, id);

            Assert.IsFalse(asRegular!.IsBye);
            Assert.AreEqual(2, asRegular.GamesWon);
            Assert.IsTrue(asBye!.IsBye);
            Assert.AreEqual(0, asBye.GamesWon);
            Assert.IsNull(asBye.WonDiceRoll);
        }

        [TestMethod]
        public async Task SetOrderAsync_NewOrder_RenumbersTheRoundsAndReportsHowManyMoved()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new MatchRepository(context);
            var second = await repository.AddAsync(1, BuildMatch("2", "Second"));
            var first = await repository.AddAsync(1, BuildMatch("1", "First"));
            var third = await repository.AddAsync(1, BuildMatch("3", "Third"));

            var moved = await repository.SetOrderAsync(1, [first, second, third]);

            var rounds = await repository.GetByEventAsync(1);
            CollectionAssert.AreEqual(new[] { "First", "Second", "Third" }, rounds.Select(m => m.OpponentDeck).ToArray());
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, rounds.Select(m => m.Sequence).ToArray());
            Assert.AreEqual(2, moved);
        }

        [TestMethod]
        public async Task SetOrderAsync_AlreadyInThatOrder_ChangesNothing()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new MatchRepository(context);
            var first = await repository.AddAsync(1, BuildMatch("1", "First"));
            var second = await repository.AddAsync(1, BuildMatch("2", "Second"));

            var moved = await repository.SetOrderAsync(1, [first, second]);

            Assert.AreEqual(0, moved);
        }

        [TestMethod]
        public async Task SetOrderAsync_IDsFromAnotherEventOrUnknown_AreIgnored()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new MatchRepository(context);
            var other = await repository.AddAsync(2, BuildMatch("1", "Other event"));
            await repository.AddAsync(2, BuildMatch("2", "Other event 2"));
            var second = await repository.AddAsync(1, BuildMatch("2", "Second"));
            var first = await repository.AddAsync(1, BuildMatch("1", "First"));

            await repository.SetOrderAsync(1, [other, 999, first, second]);

            CollectionAssert.AreEqual(new[] { "First", "Second" }, (await repository.GetByEventAsync(1)).Select(m => m.OpponentDeck).ToArray());
            CollectionAssert.AreEqual(new[] { 1, 2 }, (await repository.GetByEventAsync(2)).Select(m => m.Sequence).ToArray());
        }

        [TestMethod]
        public async Task SetOrderAsync_NullOrder_Throws()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new MatchRepository(context);

            await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => repository.SetOrderAsync(1, null!));
        }

        [TestMethod]
        public async Task UpdateAsync_ExistingRound_ChangesFieldsButKeepsPositionAndCreationDate()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new MatchRepository(context);
            await repository.AddAsync(1, BuildMatch("1", "First"));
            var id = await repository.AddAsync(1, BuildMatch("2", "Second"));
            var original = await repository.GetAsync(1, id);

            var updated = await repository.UpdateAsync(1, new Match
            {
                GamesWon = 2,
                ID = id,
                OpponentDeck = "Changed",
                Result = MatchResult.Win,
                Round = "Top 8",
                Sequence = 99
            });

            var saved = await repository.GetAsync(1, id);
            Assert.IsTrue(updated);
            Assert.AreEqual("Changed", saved!.OpponentDeck);
            Assert.AreEqual("Top 8", saved.Round);
            Assert.AreEqual(2, saved.Sequence);
            Assert.AreEqual(original!.DateCreated, saved.DateCreated);
        }

        [TestMethod]
        public async Task UpdateAsync_NullMatch_Throws()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new MatchRepository(context);

            await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => repository.UpdateAsync(1, null!));
        }

        [TestMethod]
        public async Task UpdateAsync_ResultDiffersFromScore_StoresTheChosenResult()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new MatchRepository(context);
            var id = await repository.AddAsync(1, BuildMatch("1", "First"));

            await repository.UpdateAsync(1, new Match { GamesLost = 2, GamesWon = 1, ID = id, OpponentDeck = "First", Result = MatchResult.Win, Round = "1" });

            var saved = await repository.GetAsync(1, id);
            Assert.AreEqual(MatchResult.Win, saved!.Result);
        }
        [TestMethod]
        public async Task UpdateAsync_RoundBelongsToAnotherEvent_ReturnsFalseAndChangesNothing()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new MatchRepository(context);
            var id = await repository.AddAsync(1, BuildMatch("1", "First"));

            var updated = await repository.UpdateAsync(2, new Match { ID = id, OpponentDeck = "Changed", Round = "1" });

            var saved = await repository.GetAsync(1, id);
            Assert.IsFalse(updated);
            Assert.AreEqual("First", saved!.OpponentDeck);
        }
        private static async Task AddRoundsAsync(MatchRepository repository, string opponent, int count)
        {
            for (var i = 0; i < count; i++)
                await repository.AddAsync(1, BuildMatch((i + 1).ToString(), opponent));
        }

        private static Match BuildMatch(string round, string opponent) =>
            new()
            {
                OpponentDeck = opponent,
                Result = MatchResult.Tie,
                Round = round
            };
    }
}
