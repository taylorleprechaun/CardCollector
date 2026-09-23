using CardCollector.Data;
using CardCollector.Data.Models;
using CardCollector.Repository;
using CardCollector.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;

namespace CardCollector.Tests.Repository
{
    [TestClass]
    public sealed class MatchRepositoryTests
    {
        private static readonly DateTime SeededAt = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        [TestMethod]
        public async Task AddAsync_EmptyEvent_StartsAtSequenceOne()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = await CreateRepositoryAsync(context);

            var id = await AppendAsync(repository, 1, BuildMatch("1", "Test Opponent"));

            var saved = await FindAsync(context, 1, id);
            Assert.AreEqual(1, saved!.Sequence);
        }

        [TestMethod]
        public async Task AddAsync_ExistingRounds_AppendsAfterTheLastSequence()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = await CreateRepositoryAsync(context);
            await AppendAsync(repository, 1, BuildMatch("1", "First"));
            await AppendAsync(repository, 1, BuildMatch("2", "Second"));

            var id = await AppendAsync(repository, 1, BuildMatch("3", "Third"));

            var saved = await FindAsync(context, 1, id);
            Assert.AreEqual(3, saved!.Sequence);
        }

        [TestMethod]
        public async Task AddAsync_ExistingSequenceHasGaps_LeavesTheRoundsContiguous()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = await CreateRepositoryAsync(context);
            context.Matches.AddRange(
                new Match { EventID = 1, OpponentDeck = "First", Round = "1", Sequence = 1 },
                new Match { EventID = 1, OpponentDeck = "Second", Round = "2", Sequence = 5 });
            await context.SaveChangesAsync();

            await AppendAsync(repository, 1, BuildMatch("3", "Third"));

            var rounds = await repository.GetByEventAsync(1);
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, rounds.Select(m => m.Sequence).ToArray());
            Assert.AreEqual("Third", rounds[2].OpponentDeck);
        }

        [TestMethod]
        public async Task AddAsync_EventDoesNotExist_ReturnsNullAndPersistsNothing()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = await CreateRepositoryAsync(context);

            var added = await repository.AddAsync(999, BuildMatch("1", "Test Opponent"), rounds => rounds.Count);

            Assert.IsNull(added);
            Assert.AreEqual(0, await context.Matches.CountAsync());
        }

        [TestMethod]
        public async Task AddAsync_ExistingRounds_PassesThemInPlayOrderAndReturnsTheNewRoundAtItsPosition()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = await CreateRepositoryAsync(context);
            await AppendAsync(repository, 1, BuildMatch("1", "First"));
            await AppendAsync(repository, 1, BuildMatch("3", "Third"));
            string[]? seen = null;

            var added = await repository.AddAsync(1, BuildMatch("2", "Second"), rounds =>
            {
                seen = rounds.Select(m => m.OpponentDeck).ToArray();
                return 1;
            });

            var (position, rounds) = added!.Value;
            CollectionAssert.AreEqual(new[] { "First", "Third" }, seen);
            Assert.AreEqual(1, position);
            CollectionAssert.AreEqual(new[] { "First", "Second", "Third" }, rounds.Select(m => m.OpponentDeck).ToArray());
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, rounds.Select(m => m.Sequence).ToArray());
            Assert.AreNotEqual(0, rounds[1].ID);
        }

        [TestMethod]
        public async Task AddAsync_NewRound_PersistsFieldsAndAuditDates()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = await CreateRepositoryAsync(context);
            var source = BuildMatch("Top 8", "Test Opponent");
            source.GamesLost = 1;
            source.GamesWon = 2;
            source.Notes = "line one\nline two";
            source.Result = MatchResult.Win;
            source.WonDiceRoll = true;

            var id = await AppendAsync(repository, 1, source);

            var saved = await FindAsync(context, 1, id);
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
            var repository = await CreateRepositoryAsync(context);

            await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => repository.AddAsync(1, null!, rounds => rounds.Count));
        }

        [TestMethod]
        public async Task AddAsync_NullChoosePosition_Throws()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = await CreateRepositoryAsync(context);

            await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => repository.AddAsync(1, BuildMatch("1", "Test Opponent"), null!));
        }

        [TestMethod]
        public async Task AddAsync_PositionInAnotherEvent_DoesNotMoveThatEventsRounds()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = await CreateRepositoryAsync(context);
            await AppendAsync(repository, 2, BuildMatch("1", "Other A"));
            await AppendAsync(repository, 2, BuildMatch("2", "Other B"));

            await AddAtAsync(repository, 1, BuildMatch("1", "This event"), 0);

            CollectionAssert.AreEqual(new[] { 1, 2 }, (await repository.GetByEventAsync(2)).Select(m => m.Sequence).ToArray());
        }

        [TestMethod]
        public async Task AddAsync_PositionInTheMiddle_InsertsThereAndMovesTheLaterRoundsDown()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = await CreateRepositoryAsync(context);
            await AppendAsync(repository, 1, BuildMatch("1", "First"));
            await AppendAsync(repository, 1, BuildMatch("2", "Second"));
            await AppendAsync(repository, 1, BuildMatch("4", "Fourth"));

            var id = await AddAtAsync(repository, 1, BuildMatch("3", "Third"), 2);

            var rounds = await repository.GetByEventAsync(1);
            CollectionAssert.AreEqual(new[] { "First", "Second", "Third", "Fourth" }, rounds.Select(m => m.OpponentDeck).ToArray());
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, rounds.Select(m => m.Sequence).ToArray());
            Assert.AreEqual(id, rounds[2].ID);
        }

        [TestMethod]
        public async Task AddAsync_PositionInTheMiddle_MarksOnlyTheMovedRoundsModified()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = await CreateRepositoryAsync(context);
            await SeedRoundsAsync(context, "First", "Second", "Fourth");

            await AddAtAsync(repository, 1, BuildMatch("3", "Third"), 2);

            var rounds = await repository.GetByEventAsync(1);
            Assert.AreEqual(SeededAt, rounds[0].DateModified);
            Assert.AreEqual(SeededAt, rounds[1].DateModified);
            Assert.IsTrue(rounds[3].DateModified > SeededAt);
        }

        [TestMethod]
        public async Task AddAsync_PositionOutOfRange_IsClampedToTheEnds()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = await CreateRepositoryAsync(context);
            await AppendAsync(repository, 1, BuildMatch("2", "Second"));

            await AddAtAsync(repository, 1, BuildMatch("3", "Last"), 99);
            await AddAtAsync(repository, 1, BuildMatch("1", "First"), -5);

            var rounds = await repository.GetByEventAsync(1);
            CollectionAssert.AreEqual(new[] { "First", "Second", "Last" }, rounds.Select(m => m.OpponentDeck).ToArray());
        }

        [TestMethod]
        public async Task AddAsync_PositionZero_InsertsFirst()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = await CreateRepositoryAsync(context);
            await AppendAsync(repository, 1, BuildMatch("2", "Second"));

            await AddAtAsync(repository, 1, BuildMatch("1", "First"), 0);

            var rounds = await repository.GetByEventAsync(1);
            CollectionAssert.AreEqual(new[] { "First", "Second" }, rounds.Select(m => m.OpponentDeck).ToArray());
        }
        [TestMethod]
        public async Task AddAsync_RoundInAnotherEvent_DoesNotAffectSequencing()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = await CreateRepositoryAsync(context);
            await AppendAsync(repository, 2, BuildMatch("1", "Other event"));

            var id = await AppendAsync(repository, 1, BuildMatch("1", "This event"));

            var saved = await FindAsync(context, 1, id);
            Assert.AreEqual(1, saved!.Sequence);
        }
        [TestMethod]
        public async Task DeleteAsync_LastRound_LeavesTheOthersUnchanged()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = await CreateRepositoryAsync(context);
            await AppendAsync(repository, 1, BuildMatch("1", "First"));
            await AppendAsync(repository, 1, BuildMatch("2", "Second"));
            var lastId = await AppendAsync(repository, 1, BuildMatch("3", "Third"));

            var deleted = await repository.DeleteAsync(1, lastId);

            Assert.IsNotNull(deleted);
            CollectionAssert.AreEqual(new[] { 1, 2 }, (await repository.GetByEventAsync(1)).Select(m => m.Sequence).ToArray());
        }

        [TestMethod]
        public async Task DeleteAsync_MiddleRound_MarksOnlyTheMovedRoundsModified()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = await CreateRepositoryAsync(context);
            var ids = await SeedRoundsAsync(context, "First", "Second", "Third");

            await repository.DeleteAsync(1, ids[1]);

            var remaining = await repository.GetByEventAsync(1);
            Assert.AreEqual(SeededAt, remaining[0].DateModified);
            Assert.IsTrue(remaining[1].DateModified > SeededAt);
        }

        [TestMethod]
        public async Task DeleteAsync_MiddleRound_ResequencesTheRestAndReturnsThem()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = await CreateRepositoryAsync(context);
            await AppendAsync(repository, 1, BuildMatch("1", "First"));
            var middleId = await AppendAsync(repository, 1, BuildMatch("2", "Second"));
            await AppendAsync(repository, 1, BuildMatch("3", "Third"));
            await AppendAsync(repository, 1, BuildMatch("4", "Fourth"));

            var deleted = await repository.DeleteAsync(1, middleId);

            var remaining = await repository.GetByEventAsync(1);
            CollectionAssert.AreEqual(new[] { "First", "Third", "Fourth" }, remaining.Select(m => m.OpponentDeck).ToArray());
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, remaining.Select(m => m.Sequence).ToArray());
            CollectionAssert.AreEqual(remaining.Select(m => m.ID).ToArray(), deleted!.Select(m => m.ID).ToArray());
        }

        [TestMethod]
        public async Task DeleteAsync_OnlyRound_LeavesTheEventWithNoRounds()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = await CreateRepositoryAsync(context);
            var id = await AppendAsync(repository, 1, BuildMatch("1", "First"));

            await repository.DeleteAsync(1, id);

            Assert.AreEqual(0, (await repository.GetByEventAsync(1)).Count);
        }

        [TestMethod]
        public async Task DeleteAsync_OtherEventsRounds_AreNotRenumbered()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = await CreateRepositoryAsync(context);
            var id = await AppendAsync(repository, 1, BuildMatch("1", "First"));
            await AppendAsync(repository, 2, BuildMatch("1", "Other A"));
            await AppendAsync(repository, 2, BuildMatch("2", "Other B"));

            await repository.DeleteAsync(1, id);

            CollectionAssert.AreEqual(new[] { 1, 2 }, (await repository.GetByEventAsync(2)).Select(m => m.Sequence).ToArray());
        }

        [TestMethod]
        public async Task DeleteAsync_RoundBelongsToAnotherEvent_ReturnsNullAndKeepsIt()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = await CreateRepositoryAsync(context);
            var id = await AppendAsync(repository, 1, BuildMatch("1", "First"));

            var deleted = await repository.DeleteAsync(2, id);

            Assert.IsNull(deleted);
            Assert.AreEqual(1, await context.Matches.CountAsync());
        }

        [TestMethod]
        public async Task DeleteAsync_UnknownRound_ReturnsNull()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = await CreateRepositoryAsync(context);

            var deleted = await repository.DeleteAsync(1, 999);

            Assert.IsNull(deleted);
        }

        [TestMethod]
        public async Task GetByEventAsync_RoundsAddedOutOfOrder_ReturnsPlayOrder()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = await CreateRepositoryAsync(context);
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
            var repository = await CreateRepositoryAsync(context);
            var bye = BuildMatch("1", "BYE");
            bye.IsBye = true;
            await AppendAsync(repository, 1, bye);
            await AppendAsync(repository, 1, BuildMatch("2", "Test Opponent"));

            var opponents = await repository.GetOpponentDecksAsync();

            CollectionAssert.AreEqual(new[] { "Test Opponent" }, opponents.ToArray());
        }

        [TestMethod]
        public async Task GetOpponentDecksAsync_ManyRounds_OrdersByFrequencyThenName()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = await CreateRepositoryAsync(context);
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
            var repository = await CreateRepositoryAsync(context);
            context.Matches.AddRange(Enumerable.Range(1, 205).Select(i => new Match { EventID = 1, OpponentDeck = $"Deck {i:D3}", Round = "1", Sequence = i }));
            await context.SaveChangesAsync();

            var opponents = await repository.GetOpponentDecksAsync();

            Assert.AreEqual(200, opponents.Count);
        }

        [TestMethod]
        public async Task UpdateAsync_ByeChangedToRegularRoundAndBack_PersistsEachChange()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = await CreateRepositoryAsync(context);
            var bye = BuildMatch("1", "Bye");
            bye.IsBye = true;
            bye.Result = MatchResult.Win;
            var id = await AppendAsync(repository, 1, bye);

            await repository.UpdateAsync(1, new Match { GamesLost = 1, GamesWon = 2, ID = id, IsBye = false, OpponentDeck = "Test Opponent", Result = MatchResult.Win, Round = "1", WonDiceRoll = true });
            var asRegular = await FindAsync(context, 1, id);
            await repository.UpdateAsync(1, new Match { ID = id, IsBye = true, OpponentDeck = "Bye", Result = MatchResult.Win, Round = "1" });
            var asBye = await FindAsync(context, 1, id);

            Assert.IsFalse(asRegular!.IsBye);
            Assert.AreEqual(2, asRegular.GamesWon);
            Assert.IsTrue(asBye!.IsBye);
            Assert.AreEqual(0, asBye.GamesWon);
            Assert.IsNull(asBye.WonDiceRoll);
        }

        [TestMethod]
        public async Task SetOrderAsync_NewOrder_MarksOnlyTheMovedRoundsModified()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = await CreateRepositoryAsync(context);
            var ids = await SeedRoundsAsync(context, "Second", "First", "Third");

            await repository.SetOrderAsync(1, [ids[1], ids[0], ids[2]]);

            var rounds = await repository.GetByEventAsync(1);
            Assert.IsTrue(rounds[0].DateModified > SeededAt);
            Assert.IsTrue(rounds[1].DateModified > SeededAt);
            Assert.AreEqual(SeededAt, rounds[2].DateModified);
        }

        [TestMethod]
        public async Task SetOrderAsync_NewOrder_RenumbersTheRoundsAndReportsHowManyMoved()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = await CreateRepositoryAsync(context);
            var second = await AppendAsync(repository, 1, BuildMatch("2", "Second"));
            var first = await AppendAsync(repository, 1, BuildMatch("1", "First"));
            var third = await AppendAsync(repository, 1, BuildMatch("3", "Third"));

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
            var repository = await CreateRepositoryAsync(context);
            var first = await AppendAsync(repository, 1, BuildMatch("1", "First"));
            var second = await AppendAsync(repository, 1, BuildMatch("2", "Second"));

            var moved = await repository.SetOrderAsync(1, [first, second]);

            Assert.AreEqual(0, moved);
        }

        [TestMethod]
        public async Task SetOrderAsync_IDsFromAnotherEventOrUnknown_AreIgnored()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = await CreateRepositoryAsync(context);
            var other = await AppendAsync(repository, 2, BuildMatch("1", "Other event"));
            await AppendAsync(repository, 2, BuildMatch("2", "Other event 2"));
            var second = await AppendAsync(repository, 1, BuildMatch("2", "Second"));
            var first = await AppendAsync(repository, 1, BuildMatch("1", "First"));

            await repository.SetOrderAsync(1, [other, 999, first, second]);

            CollectionAssert.AreEqual(new[] { "First", "Second" }, (await repository.GetByEventAsync(1)).Select(m => m.OpponentDeck).ToArray());
            CollectionAssert.AreEqual(new[] { 1, 2 }, (await repository.GetByEventAsync(2)).Select(m => m.Sequence).ToArray());
        }

        [TestMethod]
        public async Task SetOrderAsync_NullOrder_Throws()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = await CreateRepositoryAsync(context);

            await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => repository.SetOrderAsync(1, null!));
        }

        [TestMethod]
        public async Task UpdateAsync_ExistingRound_ChangesFieldsKeepsPositionAndReturnsTheRounds()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = await CreateRepositoryAsync(context);
            await AppendAsync(repository, 1, BuildMatch("1", "First"));
            var id = await AppendAsync(repository, 1, BuildMatch("2", "Second"));
            var original = await FindAsync(context, 1, id);

            var updated = await repository.UpdateAsync(1, new Match
            {
                GamesWon = 2,
                ID = id,
                OpponentDeck = "Changed",
                Result = MatchResult.Win,
                Round = "Top 8",
                Sequence = 99
            });

            var saved = await FindAsync(context, 1, id);
            CollectionAssert.AreEqual(new[] { "First", "Changed" }, updated!.Select(m => m.OpponentDeck).ToArray());
            Assert.AreEqual("Changed", saved!.OpponentDeck);
            Assert.AreEqual("Top 8", saved.Round);
            Assert.AreEqual(2, saved.Sequence);
            Assert.AreEqual(original!.DateCreated, saved.DateCreated);
        }

        [TestMethod]
        public async Task UpdateAsync_NullMatch_Throws()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = await CreateRepositoryAsync(context);

            await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => repository.UpdateAsync(1, null!));
        }

        [TestMethod]
        public async Task UpdateAsync_ResultDiffersFromScore_StoresTheChosenResult()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = await CreateRepositoryAsync(context);
            var id = await AppendAsync(repository, 1, BuildMatch("1", "First"));

            await repository.UpdateAsync(1, new Match { GamesLost = 2, GamesWon = 1, ID = id, OpponentDeck = "First", Result = MatchResult.Win, Round = "1" });

            var saved = await FindAsync(context, 1, id);
            Assert.AreEqual(MatchResult.Win, saved!.Result);
        }
        [TestMethod]
        public async Task UpdateAsync_RoundBelongsToAnotherEvent_ReturnsNullAndChangesNothing()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = await CreateRepositoryAsync(context);
            var id = await AppendAsync(repository, 1, BuildMatch("1", "First"));

            var updated = await repository.UpdateAsync(2, new Match { ID = id, OpponentDeck = "Changed", Round = "1" });

            var saved = await FindAsync(context, 1, id);
            Assert.IsNull(updated);
            Assert.AreEqual("First", saved!.OpponentDeck);
        }
        private static async Task<int> AddAtAsync(MatchRepository repository, int eventID, Match match, int position)
        {
            var (index, rounds) = (await repository.AddAsync(eventID, match, _ => position))!.Value;
            return rounds[index].ID;
        }

        private static async Task AddRoundsAsync(MatchRepository repository, string opponent, int count)
        {
            for (var i = 0; i < count; i++)
                await AppendAsync(repository, 1, BuildMatch((i + 1).ToString(), opponent));
        }

        private static Task<int> AppendAsync(MatchRepository repository, int eventID, Match match) =>
            AddAtAsync(repository, eventID, match, int.MaxValue);

        private static Match BuildMatch(string round, string opponent) =>
            new()
            {
                OpponentDeck = opponent,
                Result = MatchResult.Tie,
                Round = round
            };

        /// <summary>Events 1 and 2 exist; adding a round requires its event.</summary>
        private static async Task<MatchRepository> CreateRepositoryAsync(AppDBContext context)
        {
            context.Events.AddRange(
                new Event { Date = new DateOnly(2024, 5, 4), DeckName = "Sample Deck", ID = 1, Location = "Test Hobby Shop" },
                new Event { Date = new DateOnly(2024, 5, 11), DeckName = "Sample Deck", ID = 2, Location = "Test Hobby Shop" });
            await context.SaveChangesAsync();
            return new MatchRepository(context);
        }

        private static Task<Match?> FindAsync(AppDBContext context, int eventID, int id) =>
            context.Matches.AsNoTracking().FirstOrDefaultAsync(m => m.EventID == eventID && m.ID == id);

        /// <summary>Adds rounds 1 to n to event 1, last modified at <see cref="SeededAt"/>, and returns their IDs in order.</summary>
        private static async Task<IReadOnlyList<int>> SeedRoundsAsync(AppDBContext context, params string[] opponents)
        {
            var rounds = opponents
                .Select((opponent, index) => new Match
                {
                    DateCreated = SeededAt,
                    DateModified = SeededAt,
                    EventID = 1,
                    OpponentDeck = opponent,
                    Round = (index + 1).ToString(),
                    Sequence = index + 1
                })
                .ToList();
            context.Matches.AddRange(rounds);
            await context.SaveChangesAsync();
            return rounds.Select(m => m.ID).ToList();
        }
    }
}
