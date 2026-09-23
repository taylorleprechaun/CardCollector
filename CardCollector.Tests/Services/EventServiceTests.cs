using CardCollector.Data;
using CardCollector.Data.Models;
using CardCollector.Repository;
using CardCollector.Rules;
using CardCollector.Services;
using CardCollector.Tests.TestHelpers;
using CardCollector.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CardCollector.Tests.Services
{
    [TestClass]
    public sealed class EventServiceTests
    {
        [TestMethod]
        public async Task AddAsync_CallerSuppliesID_IgnoresItAndInserts()
        {
            using var context = InMemoryDbContextFactory.Create();
            var service = CreateService(context);

            var result = await service.AddAsync(BuildEvent(42, "2024-05-04"));

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(1, await context.Events.CountAsync());
        }

        [TestMethod]
        public async Task AddAsync_InvalidEvent_ReturnsErrorsAndPersistsNothing()
        {
            using var context = InMemoryDbContextFactory.Create();
            var service = CreateService(context);
            var invalid = BuildEvent(0, "2024-05-04");
            invalid.Location = "   ";

            var result = await service.AddAsync(invalid);

            Assert.IsFalse(result.Succeeded);
            CollectionAssert.Contains(result.Errors.ToArray(), "Location is required.");
            Assert.AreEqual(0, await context.Events.CountAsync());
        }

        [TestMethod]
        public async Task AddAsync_NullEvent_ThrowsArgumentNullException()
        {
            using var context = InMemoryDbContextFactory.Create();
            var service = CreateService(context);

            await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => service.AddAsync(null!));
        }

        [TestMethod]
        public async Task AddAsync_PaddedValues_PersistsTrimmedValues()
        {
            using var context = InMemoryDbContextFactory.Create();
            var service = CreateService(context);
            var padded = BuildEvent(0, "2024-05-04");
            padded.Location = "  Test Hobby Shop  ";
            padded.Notes = "   ";

            await service.AddAsync(padded);

            var saved = await context.Events.SingleAsync();
            Assert.AreEqual("Test Hobby Shop", saved.Location);
            Assert.IsNull(saved.Notes);
        }

        [TestMethod]
        public async Task DeleteAsync_EventExists_ReturnsTrue()
        {
            using var context = InMemoryDbContextFactory.Create();
            var service = CreateService(context);
            await service.AddAsync(BuildEvent(0, "2024-05-04"));
            var id = (await context.Events.SingleAsync()).ID;

            var deleted = await service.DeleteAsync(id);

            Assert.IsTrue(deleted);
            Assert.AreEqual(0, await context.Events.CountAsync());
        }

        [TestMethod]
        public async Task GetAsync_EventMissing_ReturnsNull()
        {
            using var context = InMemoryDbContextFactory.Create();
            var service = CreateService(context);

            var detail = await service.GetAsync(999);

            Assert.IsNull(detail);
        }

        [TestMethod]
        public async Task GetAsync_EventSharesUrlWithUnlinkedEvents_CountsOnlyTheOthers()
        {
            using var context = InMemoryDbContextFactory.Create();
            var service = CreateService(context);
            var eventID = AddEventWithUrl(context, "2024-01-10", "https://decks.example.test/one");
            AddEventWithUrl(context, "2024-01-17", "https://decks.example.test/one");
            AddEventWithUrl(context, "2024-01-24", "https://decks.example.test/one");
            AddEventWithUrl(context, "2024-01-31", "https://decks.example.test/one", deckID: 3);

            var detail = await service.GetAsync(eventID);

            Assert.AreEqual(2, detail!.OtherUnlinkedEventsWithSameURL);
        }

        [TestMethod]
        public async Task GetAsync_EventWithNoUrl_HasNoOtherUnlinkedEvents()
        {
            using var context = InMemoryDbContextFactory.Create();
            var service = CreateService(context);
            var eventID = AddEvent(context, "2024-01-10", "Test Hobby Shop");

            var detail = await service.GetAsync(eventID);

            Assert.AreEqual(0, detail!.OtherUnlinkedEventsWithSameURL);
        }

        [TestMethod]
        public async Task GetAsync_EventWithRounds_ReturnsRecordsFormatAndOrderedRounds()
        {
            using var context = InMemoryDbContextFactory.Create();
            AddFormat(context, "Alpha Era", "2024-01-01", null, "First", "Second");
            var id = AddEvent(context, "2024-05-04", "Test Hobby Shop");
            AddMatch(context, id, 2, MatchResult.Loss, gamesLost: 2, wonDiceRoll: false);
            AddMatch(context, id, 1, MatchResult.Win, gamesWon: 2, gamesLost: 1, wonDiceRoll: true);
            AddMatch(context, id, 3, MatchResult.Win, isBye: true);
            await context.SaveChangesAsync();
            var service = CreateService(context);

            var detail = await service.GetAsync(id);

            Assert.AreEqual("Alpha Era", detail!.Format!.Name);
            CollectionAssert.AreEqual(new[] { "First", "Second" }, detail.Format.Strategies.Select(s => s.Name).ToArray());
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, detail.Event.Matches.Select(m => m.Sequence).ToArray());
            Assert.AreEqual("2-1-0", detail.Summary.MatchRecord.ToString());
            Assert.AreEqual("2-3-0", detail.Summary.GameRecord.ToString());
            Assert.AreEqual("1-1", detail.Summary.DiceRecord.ToString());
            Assert.AreEqual(3, detail.Summary.RoundCount);
            Assert.AreEqual("4", detail.Summary.NextRound);
        }

        [TestMethod]
        public async Task GetAsync_NoFormatContainsDate_ReturnsNullFormat()
        {
            using var context = InMemoryDbContextFactory.Create();
            AddFormat(context, "Alpha Era", "2024-06-01", null);
            var id = AddEvent(context, "2024-05-04", "Test Hobby Shop");
            await context.SaveChangesAsync();
            var service = CreateService(context);

            var detail = await service.GetAsync(id);

            Assert.IsNull(detail!.Format);
        }

        [TestMethod]
        public async Task GetDeckNamesAsync_RepeatedDecks_ReturnsDistinctNames()
        {
            using var context = InMemoryDbContextFactory.Create();
            AddEvent(context, "2024-05-01", "Shop A");
            AddEvent(context, "2024-05-02", "Shop B");
            var service = CreateService(context);

            var names = await service.GetDeckNamesAsync();

            CollectionAssert.AreEqual(new[] { "Sample Deck" }, names.ToArray());
        }

        [TestMethod]
        public async Task GetLocationsAsync_RepeatedLocations_ReturnsDistinctNames()
        {
            using var context = InMemoryDbContextFactory.Create();
            AddEvent(context, "2024-05-01", "Shop B");
            AddEvent(context, "2024-05-02", "Shop A");
            AddEvent(context, "2024-05-03", "Shop B");
            var service = CreateService(context);

            var locations = await service.GetLocationsAsync();

            CollectionAssert.AreEqual(new[] { "Shop A", "Shop B" }, locations.ToArray());
        }

        [TestMethod]
        public async Task SearchAsync_EventBeforeFirstFormat_ReportsNoFormat()
        {
            using var context = InMemoryDbContextFactory.Create();
            AddFormat(context, "Alpha Era", "2024-01-01", null);
            AddEvent(context, "2023-12-31", "Too early");
            await context.SaveChangesAsync();
            var service = CreateService(context);

            var result = await service.SearchAsync(new EventSearchCriteria());

            Assert.AreEqual(FormatRules.NO_FORMAT_NAME, result.Items.Single().FormatName);
        }

        [TestMethod]
        public async Task SearchAsync_EventInOngoingFormat_ResolvesTheOngoingFormat()
        {
            using var context = InMemoryDbContextFactory.Create();
            AddFormat(context, "Ongoing Era", "2024-01-01", null);
            AddEvent(context, "2031-12-31", "Far future");
            await context.SaveChangesAsync();
            var service = CreateService(context);

            var result = await service.SearchAsync(new EventSearchCriteria());

            Assert.AreEqual("Ongoing Era", result.Items.Single().FormatName);
        }

        [TestMethod]
        public async Task SearchAsync_EventOnFormatFirstAndLastDay_ResolvesThatFormat()
        {
            using var context = InMemoryDbContextFactory.Create();
            AddFormat(context, "Earlier", "2024-01-01", "2024-02-29");
            AddFormat(context, "Alpha Era", "2024-03-01", "2024-03-31");
            AddFormat(context, "Later", "2024-04-01", null);
            AddEvent(context, "2024-03-01", "First day");
            AddEvent(context, "2024-03-31", "Last day");
            await context.SaveChangesAsync();
            var service = CreateService(context);

            var result = await service.SearchAsync(new EventSearchCriteria());

            var byLocation = result.Items.ToDictionary(i => i.Event.Location, i => i.FormatName);
            Assert.AreEqual("Alpha Era", byLocation["First day"]);
            Assert.AreEqual("Alpha Era", byLocation["Last day"]);
        }
        [TestMethod]
        public async Task SearchAsync_EventWithByeAndRounds_ComputesRecordFromStoredResults()
        {
            using var context = InMemoryDbContextFactory.Create();
            var id = AddEvent(context, "2024-05-04", "Test Hobby Shop");
            AddMatch(context, id, 1, MatchResult.Win, isBye: true);
            AddMatch(context, id, 2, MatchResult.Win, gamesWon: 2);
            AddMatch(context, id, 3, MatchResult.Loss, gamesLost: 2);
            AddMatch(context, id, 4, MatchResult.Tie, gamesWon: 1, gamesLost: 1);
            await context.SaveChangesAsync();
            var service = CreateService(context);

            var result = await service.SearchAsync(new EventSearchCriteria());

            var item = result.Items.Single();
            Assert.AreEqual("2-1-1", item.Record.ToString());
            Assert.AreEqual(4, item.RoundCount);
        }

        [TestMethod]
        public async Task SearchAsync_FormatFilter_ReturnsOnlyEventsInThatFormatsRange()
        {
            using var context = InMemoryDbContextFactory.Create();
            AddFormat(context, "Earlier", "2024-01-01", "2024-02-29");
            var alphaID = AddFormat(context, "Alpha Era", "2024-03-01", "2024-03-31");
            AddEvent(context, "2024-02-29", "Before range");
            AddEvent(context, "2024-03-01", "Range start");
            AddEvent(context, "2024-03-31", "Range end");
            AddEvent(context, "2024-04-01", "After range");
            await context.SaveChangesAsync();
            var service = CreateService(context);

            var result = await service.SearchAsync(new EventSearchCriteria { FormatID = alphaID });

            CollectionAssert.AreEquivalent(new[] { "Range start", "Range end" }, result.Items.Select(i => i.Event.Location).ToArray());
            Assert.AreEqual(2, result.TotalCount);
        }

        [TestMethod]
        public async Task SearchAsync_FormatFilterOnOngoingFormat_HasNoUpperBound()
        {
            using var context = InMemoryDbContextFactory.Create();
            var ongoingID = AddFormat(context, "Ongoing Era", "2024-03-01", null);
            AddEvent(context, "2024-02-29", "Before");
            AddEvent(context, "2030-01-01", "Much later");
            await context.SaveChangesAsync();
            var service = CreateService(context);

            var result = await service.SearchAsync(new EventSearchCriteria { FormatID = ongoingID });

            Assert.AreEqual("Much later", result.Items.Single().Event.Location);
        }

        [TestMethod]
        public async Task SearchAsync_FormatFilterOnOngoingFormatWithEndDate_UsesTheRequestedEnd()
        {
            using var context = InMemoryDbContextFactory.Create();
            var ongoingID = AddFormat(context, "Ongoing Era", "2024-03-01", null);
            AddEvent(context, "2024-03-15", "Inside");
            AddEvent(context, "2024-05-15", "After requested end");
            await context.SaveChangesAsync();
            var service = CreateService(context);

            var result = await service.SearchAsync(new EventSearchCriteria { DateTo = new DateOnly(2024, 4, 1), FormatID = ongoingID });

            Assert.AreEqual("Inside", result.Items.Single().Event.Location);
        }
        [TestMethod]
        public async Task SearchAsync_FormatFilterWithDisjointDateRange_ReturnsNothing()
        {
            using var context = InMemoryDbContextFactory.Create();
            var alphaID = AddFormat(context, "Alpha Era", "2024-03-01", "2024-03-31");
            AddEvent(context, "2024-03-15", "Inside");
            await context.SaveChangesAsync();
            var service = CreateService(context);

            var result = await service.SearchAsync(new EventSearchCriteria
            {
                DateFrom = new DateOnly(2024, 6, 1),
                FormatID = alphaID
            });

            Assert.AreEqual(0, result.Items.Count);
            Assert.AreEqual(0, result.TotalCount);
        }

        [TestMethod]
        public async Task SearchAsync_FormatFilterWithNarrowerDateRange_IntersectsBoth()
        {
            using var context = InMemoryDbContextFactory.Create();
            var alphaID = AddFormat(context, "Alpha Era", "2024-03-01", "2024-03-31");
            AddEvent(context, "2024-03-05", "Too early");
            AddEvent(context, "2024-03-15", "Inside");
            AddEvent(context, "2024-03-25", "Too late");
            await context.SaveChangesAsync();
            var service = CreateService(context);

            var result = await service.SearchAsync(new EventSearchCriteria
            {
                DateFrom = new DateOnly(2024, 3, 10),
                DateTo = new DateOnly(2024, 3, 20),
                FormatID = alphaID
            });

            Assert.AreEqual("Inside", result.Items.Single().Event.Location);
        }

        [TestMethod]
        public async Task SearchAsync_FormatFilterWithWiderDateRange_UsesTheFormatsRange()
        {
            using var context = InMemoryDbContextFactory.Create();
            var alphaID = AddFormat(context, "Alpha Era", "2024-03-01", "2024-03-31");
            AddEvent(context, "2024-02-15", "Before format");
            AddEvent(context, "2024-03-15", "Inside");
            AddEvent(context, "2024-04-15", "After format");
            await context.SaveChangesAsync();
            var service = CreateService(context);

            var result = await service.SearchAsync(new EventSearchCriteria
            {
                DateFrom = new DateOnly(2024, 1, 1),
                DateTo = new DateOnly(2024, 12, 31),
                FormatID = alphaID
            });

            Assert.AreEqual("Inside", result.Items.Single().Event.Location);
        }
        [TestMethod]
        public async Task SearchAsync_LinkedEventSharesUrlWithUnlinkedEvents_CountsAllUnlinkedEvents()
        {
            using var context = InMemoryDbContextFactory.Create();
            var service = CreateService(context);
            AddEventWithUrl(context, "2024-01-10", "https://decks.example.test/one", deckID: 3);
            AddEventWithUrl(context, "2024-01-17", "https://decks.example.test/one");
            AddEventWithUrl(context, "2024-01-24", "https://decks.example.test/one");

            var result = await service.SearchAsync(new EventSearchCriteria { Page = 1, PageSize = 25 });

            var counts = result.Items.OrderBy(i => i.Event.Date).Select(i => i.OtherUnlinkedEventsWithSameURL).ToArray();
            CollectionAssert.AreEqual(new[] { 2, 1, 1 }, counts);
        }

        [TestMethod]
        public async Task SearchAsync_NullCriteria_ThrowsArgumentNullException()
        {
            using var context = InMemoryDbContextFactory.Create();
            var service = CreateService(context);

            await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => service.SearchAsync(null!));
        }

        [TestMethod]
        public async Task SearchAsync_UnknownFormat_ReturnsNothing()
        {
            using var context = InMemoryDbContextFactory.Create();
            AddEvent(context, "2024-03-15", "Inside");
            await context.SaveChangesAsync();
            var service = CreateService(context);

            var result = await service.SearchAsync(new EventSearchCriteria { FormatID = 999, Page = 3 });

            Assert.AreEqual(0, result.Items.Count);
            Assert.AreEqual(3, result.Page);
        }
        [TestMethod]
        public async Task UpdateAsync_EventMissing_ReturnsNotFoundError()
        {
            using var context = InMemoryDbContextFactory.Create();
            var service = CreateService(context);

            var result = await service.UpdateAsync(BuildEvent(999, "2024-05-04"));

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual("Event not found.", result.Errors.Single());
        }

        [TestMethod]
        public async Task UpdateAsync_InvalidEvent_ReturnsErrorsAndLeavesEventUnchanged()
        {
            using var context = InMemoryDbContextFactory.Create();
            var id = AddEvent(context, "2024-05-04", "Original Shop");
            await context.SaveChangesAsync();
            var service = CreateService(context);
            var invalid = BuildEvent(id, "2024-05-04");
            invalid.DecklistURL = "javascript:alert(1)";
            invalid.Location = "Changed Shop";

            var result = await service.UpdateAsync(invalid);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual("Original Shop", (await context.Events.SingleAsync()).Location);
        }

        [TestMethod]
        public async Task UpdateAsync_NullEvent_ThrowsArgumentNullException()
        {
            using var context = InMemoryDbContextFactory.Create();
            var service = CreateService(context);

            await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => service.UpdateAsync(null!));
        }

        [TestMethod]
        public async Task UpdateAsync_ValidChange_PersistsIt()
        {
            using var context = InMemoryDbContextFactory.Create();
            var id = AddEvent(context, "2024-05-04", "Original Shop");
            await context.SaveChangesAsync();
            var service = CreateService(context);
            var changed = BuildEvent(id, "2024-05-04");
            changed.Location = "  Changed Shop ";

            var result = await service.UpdateAsync(changed);

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual("Changed Shop", (await context.Events.SingleAsync()).Location);
        }
        private static int AddEvent(AppDBContext context, string date, string location)
        {
            var entity = new Event
            {
                Date = DateOnly.Parse(date),
                DeckName = "Sample Deck",
                EventType = EventType.Locals,
                Location = location
            };
            context.Events.Add(entity);
            context.SaveChanges();
            return entity.ID;
        }

        private static int AddEventWithUrl(AppDBContext context, string date, string decklistUrl, int? deckID = null)
        {
            var entity = new Event
            {
                Date = DateOnly.Parse(date),
                DeckID = deckID,
                DeckName = "Sample Deck",
                DecklistURL = decklistUrl,
                EventType = EventType.Locals,
                Location = "Test Hobby Shop"
            };
            context.Events.Add(entity);
            context.SaveChanges();
            return entity.ID;
        }
        private static int AddFormat(AppDBContext context, string name, string start, string? end, params string[] strategies)
        {
            var entity = new Format
            {
                EndDate = end is null ? null : DateOnly.Parse(end),
                Name = name,
                StartDate = DateOnly.Parse(start),
                Strategies = strategies.Select((s, i) => new FormatStrategy { Name = s, Position = i }).ToList()
            };
            context.Formats.Add(entity);
            context.SaveChanges();
            return entity.ID;
        }

        private static void AddMatch(
            AppDBContext context,
            int eventID,
            int sequence,
            MatchResult result,
            int gamesLost = 0,
            int gamesWon = 0,
            bool isBye = false,
            bool? wonDiceRoll = null)
        {
            context.Matches.Add(new Match
            {
                EventID = eventID,
                GamesLost = gamesLost,
                GamesWon = gamesWon,
                IsBye = isBye,
                OpponentDeck = "Sample Opponent",
                Result = result,
                Round = sequence.ToString(),
                Sequence = sequence,
                WonDiceRoll = wonDiceRoll
            });
        }

        private static Event BuildEvent(int id, string date) =>
            new()
            {
                Date = DateOnly.Parse(date),
                DeckName = "Sample Deck",
                EventType = EventType.Locals,
                ID = id,
                Location = "Test Hobby Shop"
            };

        private static EventService CreateService(AppDBContext context) =>
            new(new EventRepository(context), new FormatService(new FormatRepository(context)));
    }
}
