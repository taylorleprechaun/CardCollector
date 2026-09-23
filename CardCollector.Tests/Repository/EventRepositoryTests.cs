using CardCollector.Data;
using CardCollector.Data.Models;
using CardCollector.Repository;
using CardCollector.Tests.TestHelpers;
using CardCollector.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CardCollector.Tests.Repository
{
    [TestClass]
    public sealed class EventRepositoryTests
    {
        [TestMethod]
        public async Task AddAsync_EventCarriesRounds_IgnoresTheRounds()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new EventRepository(context);
            var source = BuildEvent("2024-05-04", "Test Hobby Shop", "Sample Deck");
            source.Matches = [BuildMatch(1)];

            await repository.AddAsync(source);

            Assert.AreEqual(0, await context.Matches.CountAsync());
        }

        [TestMethod]
        public async Task AddAsync_NewEvent_PersistsFieldsAndAuditDates()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new EventRepository(context);
            var source = BuildEvent("2024-05-04", "Test Hobby Shop", "Sample Deck", EventType.Regional);
            source.Finish = 2;
            source.FinishNote = "note";
            source.Notes = "line one\nline two";
            source.Players = 16;
            source.TopCut = "Top 8";

            var id = await repository.AddAsync(source);

            var saved = await repository.GetAsync(id, includeMatches: false);
            Assert.AreEqual(new DateOnly(2024, 5, 4), saved!.Date);
            Assert.AreEqual("Test Hobby Shop", saved.Location);
            Assert.AreEqual(EventType.Regional, saved.EventType);
            Assert.AreEqual(2, saved.Finish);
            Assert.AreEqual("line one\nline two", saved.Notes);
            Assert.AreNotEqual(default, saved.DateCreated);
            Assert.AreEqual(saved.DateCreated, saved.DateModified);
        }
        [TestMethod]
        public async Task AddAsync_NullEvent_ThrowsArgumentNullException()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new EventRepository(context);

            await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => repository.AddAsync(null!));
        }

        [TestMethod]
        public async Task DeleteAsync_EventMissing_ReturnsFalse()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new EventRepository(context);

            var deleted = await repository.DeleteAsync(999);

            Assert.IsFalse(deleted);
        }

        [TestMethod]
        public async Task DeleteAsync_EventWithRounds_RemovesTheEventAndItsRounds()
        {
            var (context, connection) = InMemoryDbContextFactory.CreateSqlite();
            using var _ = connection;
            using var __ = context;
            var repository = new EventRepository(context);
            var keptID = await repository.AddAsync(BuildEvent("2024-05-05", "Kept Shop", "Sample Deck"));
            var deletedID = await repository.AddAsync(BuildEvent("2024-05-04", "Deleted Shop", "Sample Deck"));
            await AddMatchesAsync(context, keptID, 1);
            await AddMatchesAsync(context, deletedID, 1, 2, 3);

            var deleted = await repository.DeleteAsync(deletedID);

            Assert.IsTrue(deleted);
            Assert.AreEqual(1, await context.Events.CountAsync());
            Assert.AreEqual(1, await context.Matches.CountAsync());
            Assert.AreEqual(keptID, (await context.Matches.SingleAsync()).EventID);
        }
        [TestMethod]
        public async Task GetAllWithMatchesAsync_NoEvents_ReturnsEmpty()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new EventRepository(context);

            var events = await repository.GetAllWithMatchesAsync();

            Assert.AreEqual(0, events.Count);
        }

        [TestMethod]
        public async Task GetAllWithMatchesAsync_SeveralEvents_ReturnsEveryEventNewestFirstWithRoundsInPlayOrder()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new EventRepository(context);
            var older = await AddEventAsync(context, "2024-01-10");
            var newer = await AddEventAsync(context, "2024-03-10");
            await AddMatchesAsync(context, older, 2, 1);
            await AddMatchesAsync(context, newer, 3, 1, 2);

            var events = await repository.GetAllWithMatchesAsync();

            CollectionAssert.AreEqual(new[] { newer, older }, events.Select(e => e.ID).ToArray());
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, events[0].Matches.Select(m => m.Sequence).ToArray());
            CollectionAssert.AreEqual(new[] { 1, 2 }, events[1].Matches.Select(m => m.Sequence).ToArray());
        }
        [TestMethod]
        public async Task GetAsync_EventMissing_ReturnsNull()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new EventRepository(context);

            var loaded = await repository.GetAsync(999, includeMatches: true);

            Assert.IsNull(loaded);
        }

        [TestMethod]
        public async Task GetAsync_ExcludeMatches_ReturnsNoRounds()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new EventRepository(context);
            var id = await repository.AddAsync(BuildEvent("2024-05-04", "Test Hobby Shop", "Sample Deck"));
            await AddMatchesAsync(context, id, 1);

            var loaded = await repository.GetAsync(id, includeMatches: false);

            Assert.AreEqual(0, loaded!.Matches.Count);
        }

        [TestMethod]
        public async Task GetAsync_IncludeMatches_ReturnsRoundsInPlayOrder()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new EventRepository(context);
            var id = await repository.AddAsync(BuildEvent("2024-05-04", "Test Hobby Shop", "Sample Deck"));
            await AddMatchesAsync(context, id, 3, 1, 2);

            var loaded = await repository.GetAsync(id, includeMatches: true);

            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, loaded!.Matches.Select(m => m.Sequence).ToArray());
        }
        [TestMethod]
        public async Task GetByDeckAsync_EventsOfSeveralDecks_ReturnsOnlyTheDecksEventsNewestFirst()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new EventRepository(context);
            var older = await AddEventAsync(context, "2024-01-10", deckID: 7);
            var newer = await AddEventAsync(context, "2024-03-10", deckID: 7);
            await AddEventAsync(context, "2024-02-10", deckID: 8);
            await AddEventAsync(context, "2024-02-11");

            var events = await repository.GetByDeckAsync(7);

            CollectionAssert.AreEqual(new[] { newer, older }, events.Select(e => e.ID).ToArray());
        }

        [TestMethod]
        public async Task GetDeckNamesAsync_RepeatedDecks_ReturnsDistinctSortedNames()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new EventRepository(context);
            await repository.AddAsync(BuildEvent("2024-05-01", "Shop A", "Zebra Deck"));
            await repository.AddAsync(BuildEvent("2024-05-02", "Shop B", "Alpha Deck"));
            await repository.AddAsync(BuildEvent("2024-05-03", "Shop C", "Zebra Deck"));

            var names = await repository.GetDeckNamesAsync();

            CollectionAssert.AreEqual(new[] { "Alpha Deck", "Zebra Deck" }, names.ToArray());
        }

        [TestMethod]
        public async Task GetLocationsAsync_RepeatedLocations_ReturnsDistinctSortedNames()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new EventRepository(context);
            await repository.AddAsync(BuildEvent("2024-05-01", "Zebra Shop", "Sample Deck"));
            await repository.AddAsync(BuildEvent("2024-05-02", "Alpha Shop", "Sample Deck"));
            await repository.AddAsync(BuildEvent("2024-05-03", "Zebra Shop", "Sample Deck"));

            var locations = await repository.GetLocationsAsync();

            CollectionAssert.AreEqual(new[] { "Alpha Shop", "Zebra Shop" }, locations.ToArray());
        }

        [TestMethod]
        public async Task GetUnlinkedByDecklistUrlAsync_BlankUrl_ThrowsArgumentException()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new EventRepository(context);

            await Assert.ThrowsExactlyAsync<ArgumentException>(() => repository.GetUnlinkedByDecklistUrlAsync("  ", 1));
        }

        [TestMethod]
        public async Task GetUnlinkedByDecklistUrlAsync_SharedUrl_ReturnsOnlyOtherEventsWithNoDeck()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new EventRepository(context);
            const string url = "https://decks.example.test/one";
            var excluded = await AddEventAsync(context, "2024-01-10", url);
            var unlinkedPeer = await AddEventAsync(context, "2024-01-17", url);
            await AddEventAsync(context, "2024-01-24", url, deckID: 3);
            await AddEventAsync(context, "2024-01-31", "https://decks.example.test/two");
            await AddEventAsync(context, "2024-02-07");

            var events = await repository.GetUnlinkedByDecklistUrlAsync(url, excluded);

            CollectionAssert.AreEqual(new[] { unlinkedPeer }, events.Select(e => e.ID).ToArray());
        }

        [TestMethod]
        public async Task GetUnlinkedUrlCountsAsync_NoUrls_ReturnsEmpty()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new EventRepository(context);
            await AddEventAsync(context, "2024-01-10", "https://decks.example.test/one");

            var counts = await repository.GetUnlinkedUrlCountsAsync([]);

            Assert.AreEqual(0, counts.Count);
        }

        [TestMethod]
        public async Task GetUnlinkedUrlCountsAsync_NullUrls_ThrowsArgumentNullException()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new EventRepository(context);

            await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => repository.GetUnlinkedUrlCountsAsync(null!));
        }

        [TestMethod]
        public async Task GetUnlinkedUrlCountsAsync_SharedUrls_CountsOnlyEventsWithNoDeck()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new EventRepository(context);
            await AddEventAsync(context, "2024-01-10", "https://decks.example.test/one");
            await AddEventAsync(context, "2024-01-17", "https://decks.example.test/one");
            await AddEventAsync(context, "2024-01-24", "https://decks.example.test/one", deckID: 3);
            await AddEventAsync(context, "2024-01-31", "https://decks.example.test/two", deckID: 3);
            await AddEventAsync(context, "2024-02-07", "https://decks.example.test/other");

            var counts = await repository.GetUnlinkedUrlCountsAsync(["https://decks.example.test/one", "https://decks.example.test/two"]);

            Assert.AreEqual(1, counts.Count);
            Assert.AreEqual(2, counts["https://decks.example.test/one"]);
        }

        [TestMethod]
        public async Task SearchAsync_CombinedFilters_AppliesAllOfThem()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new EventRepository(context);
            await repository.AddAsync(BuildEvent("2024-05-01", "Alpha Games", "Sample Deck", EventType.Locals));
            var expectedID = await repository.AddAsync(BuildEvent("2024-05-10", "Alpha Games", "Sample Deck", EventType.Locals));
            await repository.AddAsync(BuildEvent("2024-05-10", "Alpha Games", "Other Deck", EventType.Locals));
            await repository.AddAsync(BuildEvent("2024-05-10", "Beta Games", "Sample Deck", EventType.Locals));
            await repository.AddAsync(BuildEvent("2024-05-10", "Alpha Games", "Sample Deck", EventType.Regional));

            var result = await repository.SearchAsync(new EventSearchCriteria
            {
                DateFrom = new DateOnly(2024, 5, 5),
                DeckName = "sample",
                EventType = EventType.Locals,
                Location = "alpha"
            });

            Assert.AreEqual(1, result.TotalCount);
            Assert.AreEqual(expectedID, result.Items.Single().ID);
        }

        [TestMethod]
        public async Task SearchAsync_DateRange_IncludesBothBoundaryDays()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new EventRepository(context);
            await repository.AddAsync(BuildEvent("2024-05-01", "Before", "Sample Deck"));
            await repository.AddAsync(BuildEvent("2024-05-02", "First day", "Sample Deck"));
            await repository.AddAsync(BuildEvent("2024-05-03", "Last day", "Sample Deck"));
            await repository.AddAsync(BuildEvent("2024-05-04", "After", "Sample Deck"));

            var result = await repository.SearchAsync(new EventSearchCriteria
            {
                DateFrom = new DateOnly(2024, 5, 2),
                DateTo = new DateOnly(2024, 5, 3)
            });

            CollectionAssert.AreEqual(new[] { "Last day", "First day" }, result.Items.Select(e => e.Location).ToArray());
        }

        [TestMethod]
        public async Task SearchAsync_DeckFilter_MatchesCaseInsensitiveSubstring()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new EventRepository(context);
            await repository.AddAsync(BuildEvent("2024-05-01", "Shop A", "Blue Sample Deck"));
            await repository.AddAsync(BuildEvent("2024-05-02", "Shop B", "Other Deck"));

            var result = await repository.SearchAsync(new EventSearchCriteria { DeckName = "SAMPLE" });

            Assert.AreEqual("Blue Sample Deck", result.Items.Single().DeckName);
        }

        [TestMethod]
        public async Task SearchAsync_EventTypeFilter_ReturnsOnlyThatType()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new EventRepository(context);
            await repository.AddAsync(BuildEvent("2024-05-01", "Shop A", "Sample Deck", EventType.Locals));
            await repository.AddAsync(BuildEvent("2024-05-02", "Shop B", "Sample Deck", EventType.YCS));

            var result = await repository.SearchAsync(new EventSearchCriteria { EventType = EventType.YCS });

            Assert.AreEqual("Shop B", result.Items.Single().Location);
        }

        [TestMethod]
        public async Task SearchAsync_InvalidPaging_FallsBackToFirstPageOfDefaultSize()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new EventRepository(context);
            await repository.AddAsync(BuildEvent("2024-05-01", "Shop", "Sample Deck"));

            var result = await repository.SearchAsync(new EventSearchCriteria { Page = 0, PageSize = 0 });

            Assert.AreEqual(1, result.Page);
            Assert.AreEqual(25, result.PageSize);
            Assert.AreEqual(1, result.Items.Count);
        }

        [TestMethod]
        public async Task SearchAsync_LocationFilter_MatchesCaseInsensitiveSubstring()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new EventRepository(context);
            await repository.AddAsync(BuildEvent("2024-05-01", "Test Hobby Shop", "Sample Deck"));
            await repository.AddAsync(BuildEvent("2024-05-02", "Other Place", "Sample Deck"));

            var result = await repository.SearchAsync(new EventSearchCriteria { Location = "hobby" });

            Assert.AreEqual("Test Hobby Shop", result.Items.Single().Location);
        }

        [TestMethod]
        public async Task SearchAsync_LocationFilter_TreatsWildcardCharactersLiterally()
        {
            var (context, connection) = InMemoryDbContextFactory.CreateSqlite();
            using var _ = connection;
            using var __ = context;
            var repository = new EventRepository(context);
            await repository.AddAsync(BuildEvent("2024-05-01", "100% Games", "Sample Deck"));
            await repository.AddAsync(BuildEvent("2024-05-02", "1000 Games", "Sample Deck"));
            await repository.AddAsync(BuildEvent("2024-05-03", "Under_score Cards", "Sample Deck"));
            await repository.AddAsync(BuildEvent("2024-05-04", "UnderXscore Cards", "Sample Deck"));

            var percent = await repository.SearchAsync(new EventSearchCriteria { Location = "100%" });
            var underscore = await repository.SearchAsync(new EventSearchCriteria { Location = "Under_score" });

            Assert.AreEqual("100% Games", percent.Items.Single().Location);
            Assert.AreEqual("Under_score Cards", underscore.Items.Single().Location);
        }
        [TestMethod]
        public async Task SearchAsync_NoFilters_ReturnsEventsNewestFirstWithRounds()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new EventRepository(context);
            await repository.AddAsync(BuildEvent("2024-05-01", "Oldest", "Sample Deck"));
            var newestID = await repository.AddAsync(BuildEvent("2024-05-03", "Newest", "Sample Deck"));
            await repository.AddAsync(BuildEvent("2024-05-02", "Middle", "Sample Deck"));
            await AddMatchesAsync(context, newestID, 2, 1);

            var result = await repository.SearchAsync(new EventSearchCriteria());

            CollectionAssert.AreEqual(new[] { "Newest", "Middle", "Oldest" }, result.Items.Select(e => e.Location).ToArray());
            CollectionAssert.AreEqual(new[] { 1, 2 }, result.Items[0].Matches.Select(m => m.Sequence).ToArray());
        }

        [TestMethod]
        public async Task SearchAsync_NullCriteria_ThrowsArgumentNullException()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new EventRepository(context);

            await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => repository.SearchAsync(null!));
        }

        [TestMethod]
        public async Task SearchAsync_Pagination_ReturnsRequestedPageAndTotals()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new EventRepository(context);
            for (var day = 1; day <= 5; day++)
                await repository.AddAsync(BuildEvent($"2024-05-0{day}", $"Shop {day}", "Sample Deck"));

            var result = await repository.SearchAsync(new EventSearchCriteria { Page = 2, PageSize = 2 });

            CollectionAssert.AreEqual(new[] { "Shop 3", "Shop 2" }, result.Items.Select(e => e.Location).ToArray());
            Assert.AreEqual(5, result.TotalCount);
            Assert.AreEqual(3, result.TotalPages);
            Assert.AreEqual(2, result.Page);
            Assert.AreEqual(2, result.PageSize);
        }
        [TestMethod]
        public async Task SearchAsync_SameDateEvents_AreBothReturnedNewestIDFirst()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new EventRepository(context);
            var firstID = await repository.AddAsync(BuildEvent("2024-05-04", "Same Shop", "Sample Deck"));
            var secondID = await repository.AddAsync(BuildEvent("2024-05-04", "Same Shop", "Sample Deck"));

            var result = await repository.SearchAsync(new EventSearchCriteria());

            CollectionAssert.AreEqual(new[] { secondID, firstID }, result.Items.Select(e => e.ID).ToArray());
        }
        [TestMethod]
        public async Task SetDeckAsync_DeckIDIsNull_ClearsTheLink()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new EventRepository(context);
            var linked = await AddEventAsync(context, "2024-01-10", deckID: 5);

            var count = await repository.SetDeckAsync([linked], null);

            Assert.AreEqual(1, count);
            Assert.IsNull((await repository.GetAsync(linked, includeMatches: false))!.DeckID);
        }

        [TestMethod]
        public async Task SetDeckAsync_EventsAndAMissingID_SetsTheExistingOnesAndCountsThem()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new EventRepository(context);
            var first = await AddEventAsync(context, "2024-01-10");
            var second = await AddEventAsync(context, "2024-01-17");
            var untouched = await AddEventAsync(context, "2024-01-24");

            var count = await repository.SetDeckAsync([first, second, 9999], 5);

            Assert.AreEqual(2, count);
            Assert.AreEqual(5, (await repository.GetAsync(first, includeMatches: false))!.DeckID);
            Assert.AreEqual(5, (await repository.GetAsync(second, includeMatches: false))!.DeckID);
            Assert.IsNull((await repository.GetAsync(untouched, includeMatches: false))!.DeckID);
        }

        [TestMethod]
        public async Task SetDeckAsync_NoEventIDs_ReturnsZero()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new EventRepository(context);

            Assert.AreEqual(0, await repository.SetDeckAsync([], 5));
        }

        [TestMethod]
        public async Task SetDeckAsync_NullEventIDs_ThrowsArgumentNullException()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new EventRepository(context);

            await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => repository.SetDeckAsync(null!, 5));
        }

        [TestMethod]
        public async Task UpdateAsync_EventMissing_ReturnsFalse()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new EventRepository(context);
            var missing = BuildEvent("2024-05-04", "Shop", "Sample Deck");
            missing.ID = 999;

            var updated = await repository.UpdateAsync(missing);

            Assert.IsFalse(updated);
        }

        [TestMethod]
        public async Task UpdateAsync_ExistingEvent_ChangesFieldsButKeepsRoundsAndDeckLink()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new EventRepository(context);
            var id = await repository.AddAsync(BuildEvent("2024-05-04", "Old Shop", "Old Deck"));
            await AddMatchesAsync(context, id, 1, 2);
            var stored = await context.Events.SingleAsync();
            stored.DeckID = 12;
            await context.SaveChangesAsync();
            var changes = BuildEvent("2024-06-01", "New Shop", "New Deck", EventType.YCS);
            changes.ID = id;
            changes.Players = 40;

            var updated = await repository.UpdateAsync(changes);

            var saved = await repository.GetAsync(id, includeMatches: true);
            Assert.IsTrue(updated);
            Assert.AreEqual("New Shop", saved!.Location);
            Assert.AreEqual(EventType.YCS, saved.EventType);
            Assert.AreEqual(40, saved.Players);
            Assert.AreEqual(12, saved.DeckID);
            Assert.AreEqual(2, saved.Matches.Count);
        }
        [TestMethod]
        public async Task UpdateAsync_NullEvent_ThrowsArgumentNullException()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new EventRepository(context);

            await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => repository.UpdateAsync(null!));
        }
        private static async Task<int> AddEventAsync(AppDBContext context, string date, string? decklistUrl = null, int? deckID = null)
        {
            var tournamentEvent = BuildEvent(date, "Test Hobby Shop", "Sample Deck");
            tournamentEvent.DecklistURL = decklistUrl;
            tournamentEvent.DeckID = deckID;
            context.Events.Add(tournamentEvent);
            await context.SaveChangesAsync();
            return tournamentEvent.ID;
        }

        private static async Task AddMatchesAsync(AppDBContext context, int eventID, params int[] sequences)
        {
            foreach (var sequence in sequences)
            {
                var match = BuildMatch(sequence);
                match.EventID = eventID;
                context.Matches.Add(match);
            }

            await context.SaveChangesAsync();
        }

        private static Event BuildEvent(string date, string location, string deckName, EventType eventType = EventType.Locals) =>
            new()
            {
                Date = DateOnly.Parse(date),
                DeckName = deckName,
                EventType = eventType,
                Location = location
            };

        private static Match BuildMatch(int sequence) =>
            new()
            {
                OpponentDeck = "Sample Opponent",
                Result = MatchResult.Win,
                Round = sequence.ToString(),
                Sequence = sequence
            };
    }
}
