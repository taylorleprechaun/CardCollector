using CardCollector.Data;
using CardCollector.Data.Models;
using CardCollector.Repository;
using CardCollector.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;

namespace CardCollector.Tests.Repository
{
    [TestClass]
    public sealed class DeckRepositoryTests
    {
        [TestMethod]
        public async Task AddAsync_DeckWithCards_PersistsCardsAndAuditDates()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new DeckRepository(context);
            var source = BuildDeck("Sample Deck", Card(100, DeckSection.Main, 3, 0), Card(200, DeckSection.Extra, 1, 0));
            source.Notes = "a note";

            var id = await repository.AddAsync(source);

            var saved = await repository.GetAsync(id, includeCards: true);
            Assert.AreEqual("Sample Deck", saved!.Name);
            Assert.AreEqual("a note", saved.Notes);
            Assert.AreEqual(2, saved.Cards.Count);
            Assert.AreNotEqual(default, saved.DateCreated);
            Assert.AreEqual(saved.DateCreated, saved.DateModified);
        }

        [TestMethod]
        public async Task AddAsync_NullDeck_ThrowsArgumentNullException()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new DeckRepository(context);

            await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => repository.AddAsync(null!));
        }

        [TestMethod]
        public async Task DeleteAsync_DeckMissing_ReturnsFalse()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new DeckRepository(context);

            Assert.IsFalse(await repository.DeleteAsync(99));
        }

        [TestMethod]
        public async Task DeleteAsync_DeckUsedByEvents_ClearsThoseEventsAndLeavesNoOrphanCards()
        {
            var (context, connection) = InMemoryDbContextFactory.CreateSqlite();
            using var _ = context;
            using var __ = connection;
            var repository = new DeckRepository(context);
            var doomed = await repository.AddAsync(BuildDeck("Doomed", Card(100, DeckSection.Main, 2, 0), Card(200, DeckSection.Side, 1, 0)));
            var kept = await repository.AddAsync(BuildDeck("Kept", Card(300, DeckSection.Main, 1, 0)));
            var firstEvent = await AddEventAsync(context, doomed);
            var secondEvent = await AddEventAsync(context, doomed);
            var otherDeckEvent = await AddEventAsync(context, kept);
            var noDeckEvent = await AddEventAsync(context, null);

            var deleted = await repository.DeleteAsync(doomed);

            Assert.IsTrue(deleted);
            context.ChangeTracker.Clear();
            Assert.AreEqual(1, await context.Decks.CountAsync());
            Assert.AreEqual(1, await context.DeckCards.CountAsync());
            Assert.AreEqual(4, await context.Events.CountAsync());
            Assert.IsNull((await context.Events.SingleAsync(e => e.ID == firstEvent)).DeckID);
            Assert.IsNull((await context.Events.SingleAsync(e => e.ID == secondEvent)).DeckID);
            Assert.AreEqual(kept, (await context.Events.SingleAsync(e => e.ID == otherDeckEvent)).DeckID);
            Assert.IsNull((await context.Events.SingleAsync(e => e.ID == noDeckEvent)).DeckID);
        }

        [TestMethod]
        public async Task GetAllAsync_Decks_ReturnsCopiesPerSectionAndEventCountsSortedByName()
        {
            var (context, connection) = InMemoryDbContextFactory.CreateSqlite();
            using var _ = context;
            using var __ = connection;
            var repository = new DeckRepository(context);
            var zebra = await repository.AddAsync(BuildDeck("Zebra Deck", Card(1, DeckSection.Main, 3, 0), Card(2, DeckSection.Main, 2, 1), Card(3, DeckSection.Extra, 1, 0)));
            var alpha = await repository.AddAsync(BuildDeck("Alpha Deck", Card(4, DeckSection.Side, 2, 0)));
            await AddEventAsync(context, zebra);
            await AddEventAsync(context, zebra);

            var decks = await repository.GetAllAsync();

            CollectionAssert.AreEqual(new[] { alpha, zebra }, decks.Select(d => d.ID).ToArray());
            Assert.AreEqual(0, decks[0].MainCount);
            Assert.AreEqual(2, decks[0].SideCount);
            Assert.AreEqual(0, decks[0].EventCount);
            Assert.AreEqual(5, decks[1].MainCount);
            Assert.AreEqual(1, decks[1].ExtraCount);
            Assert.AreEqual(0, decks[1].SideCount);
            Assert.AreEqual(2, decks[1].EventCount);
        }

        [TestMethod]
        public async Task GetAsync_DeckMissing_ReturnsNull()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new DeckRepository(context);

            Assert.IsNull(await repository.GetAsync(99, includeCards: true));
        }

        [TestMethod]
        public async Task GetAsync_IncludeCards_ReturnsCardsInSortOrder()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new DeckRepository(context);
            var id = await repository.AddAsync(BuildDeck("Sample Deck", Card(300, DeckSection.Main, 1, 2), Card(100, DeckSection.Main, 1, 0), Card(200, DeckSection.Main, 1, 1)));

            var deck = await repository.GetAsync(id, includeCards: true);

            CollectionAssert.AreEqual(new[] { 100, 200, 300 }, deck!.Cards.Select(c => c.CardID).ToArray());
        }

        [TestMethod]
        public async Task GetAsync_WithoutCards_ReturnsNoCards()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new DeckRepository(context);
            var id = await repository.AddAsync(BuildDeck("Sample Deck", Card(100, DeckSection.Main, 1, 0)));

            var deck = await repository.GetAsync(id, includeCards: false);

            Assert.AreEqual(0, deck!.Cards.Count);
        }

        [TestMethod]
        public async Task UpdateAsync_DeckExists_ChangesNameAndNotesButNotCards()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new DeckRepository(context);
            var id = await repository.AddAsync(BuildDeck("Old Name", Card(100, DeckSection.Main, 2, 0)));

            var updated = await repository.UpdateAsync(new Deck { ID = id, Name = "New Name", Notes = "new note" });

            Assert.IsTrue(updated);
            var deck = await repository.GetAsync(id, includeCards: true);
            Assert.AreEqual("New Name", deck!.Name);
            Assert.AreEqual("new note", deck.Notes);
            Assert.AreEqual(2, deck.Cards.Single().Quantity);
        }

        [TestMethod]
        public async Task UpdateAsync_DeckMissing_ReturnsFalse()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new DeckRepository(context);

            Assert.IsFalse(await repository.UpdateAsync(new Deck { ID = 99, Name = "Whatever" }));
        }

        [TestMethod]
        public async Task UpdateAsync_NullDeck_ThrowsArgumentNullException()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new DeckRepository(context);

            await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => repository.UpdateAsync(null!));
        }

        private static async Task<int> AddEventAsync(AppDBContext context, int? deckID)
        {
            var tournamentEvent = new Event
            {
                Date = new DateOnly(2024, 5, 4),
                DateCreated = DateTime.UtcNow,
                DateModified = DateTime.UtcNow,
                DeckID = deckID,
                DeckName = "Sample Deck",
                Location = "Test Hobby Shop"
            };
            context.Events.Add(tournamentEvent);
            await context.SaveChangesAsync();
            return tournamentEvent.ID;
        }

        private static Deck BuildDeck(string name, params DeckCard[] cards) =>
            new() { Cards = cards, Name = name };

        private static DeckCard Card(int cardID, DeckSection section, int quantity, int sortOrder) =>
            new() { CardID = cardID, Quantity = quantity, Section = section, SortOrder = sortOrder };
    }
}
