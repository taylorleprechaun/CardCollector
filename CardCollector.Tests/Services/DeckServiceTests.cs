using CardCollector.Data;
using CardCollector.Data.Models;
using CardCollector.DTO;
using CardCollector.Models;
using CardCollector.Repository;
using CardCollector.Services;
using CardCollector.Tests.TestHelpers;
using CardCollector.ViewModels;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace CardCollector.Tests.Services
{
    [TestClass]
    public sealed class DeckServiceTests
    {
        private const string SHARED_URL = "https://decks.example.test/shared";

        [TestMethod]
        public async Task DeleteAsync_DeckExists_ReturnsTrueAndClearsItsEvents()
        {
            using var context = InMemoryDbContextFactory.Create();
            var service = CreateService(context);
            var deckID = (await service.ImportAsync(new DeckImportRequest { Name = "Sample", Text = Ydk(100) })).DeckID;
            var eventID = await AddEventAsync(context, deckID: deckID);

            var deleted = await service.DeleteAsync(deckID);

            Assert.IsTrue(deleted);
            Assert.IsNull((await context.Events.SingleAsync(e => e.ID == eventID)).DeckID);
        }

        [TestMethod]
        public async Task DeleteAsync_DeckMissing_ReturnsFalse()
        {
            using var context = InMemoryDbContextFactory.Create();
            var service = CreateService(context);

            Assert.IsFalse(await service.DeleteAsync(99));
        }

        [TestMethod]
        public async Task GetAllAsync_DeckImported_ReturnsItWithItsCardCounts()
        {
            using var context = InMemoryDbContextFactory.Create();
            var service = CreateService(context);
            await service.ImportAsync(new DeckImportRequest { Name = "Sample Deck", Text = Ydk(100) });

            var decks = await service.GetAllAsync();

            Assert.AreEqual("Sample Deck", decks.Single().Name);
            Assert.AreEqual(1, decks.Single().MainCount);
        }

        [TestMethod]
        public async Task GetAsync_DeckMissing_ReturnsNull()
        {
            using var context = InMemoryDbContextFactory.Create();
            var service = CreateService(context);

            Assert.IsNull(await service.GetAsync(99));
        }

        [TestMethod]
        public async Task GetAsync_DeckWithCards_SortsSectionsByNameAndFlagsUnknownCards()
        {
            using var context = InMemoryDbContextFactory.Create();
            var names = new Dictionary<int, string> { [100] = "Alpha", [200] = "Zulu" };
            var service = CreateService(context, unknownCardIDs: [300], names: names);
            var eventID = await AddEventAsync(context);
            var text = "#main\n200\n100\n100\n300\n#extra\n900\n!side\n800\n";
            var deckID = (await service.ImportAsync(new DeckImportRequest { EventID = eventID, Text = text })).DeckID;

            var detail = await service.GetAsync(deckID);

            // Pasted as 200, 100, 300: the display order comes from the sort, with the unknown card last.
            CollectionAssert.AreEqual(new[] { 100, 200, 300 }, detail!.Main.Cards.Select(c => c.CardID).ToArray());
            Assert.AreEqual(4, detail.Main.Count);
            Assert.AreEqual(1, detail.Extra.Count);
            Assert.AreEqual(1, detail.Side.Count);
            Assert.AreEqual(1, detail.UnknownCardCount);
            Assert.IsTrue(detail.Main.Cards.Single(c => c.CardID == 300).IsUnknown);
            Assert.AreEqual(3, detail.MainTypes.Monsters);
            Assert.AreEqual(eventID, detail.Events.Single().ID);
        }

        [TestMethod]
        public async Task GetOptionsAsync_DeckImported_ReturnsItsIDAndName()
        {
            using var context = InMemoryDbContextFactory.Create();
            var service = CreateService(context);
            var deckID = (await service.ImportAsync(new DeckImportRequest { Name = "Sample Deck", Text = Ydk(100) })).DeckID;

            var options = await service.GetOptionsAsync();

            Assert.AreEqual(new DeckOption(deckID, "Sample Deck"), options.Single());
        }

        [TestMethod]
        public async Task ImportAsync_AliasedPasscode_IsStoredAsTheAppCardID()
        {
            using var context = InMemoryDbContextFactory.Create();
            var service = CreateService(context, aliases: new Dictionary<int, int> { [83764719] = 83764718 });

            var result = await service.ImportAsync(new DeckImportRequest { Name = "Sample", Text = Ydk(83764718, 83764719) });

            var card = await context.DeckCards.SingleAsync();
            Assert.AreEqual(83764718, card.CardID);
            Assert.AreEqual(2, card.Quantity);
            Assert.AreEqual(0, result.UnknownPasscodes.Count);
        }

        [TestMethod]
        public async Task ImportAsync_EventMissing_ReturnsFailureAndStoresNothing()
        {
            using var context = InMemoryDbContextFactory.Create();
            var service = CreateService(context);

            var result = await service.ImportAsync(new DeckImportRequest { EventID = 99, Text = Ydk(100) });

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(0, await context.Decks.CountAsync());
        }

        [TestMethod]
        public async Task ImportAsync_InvalidText_ReturnsFailureAndStoresNothing()
        {
            using var context = InMemoryDbContextFactory.Create();
            var service = CreateService(context);

            var result = await service.ImportAsync(new DeckImportRequest { Name = "Sample", Text = "not a deck" });

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(0, await context.Decks.CountAsync());
        }

        [TestMethod]
        public async Task ImportAsync_LinkingFails_RollsBackTheDeck()
        {
            var (context, connection) = InMemoryDbContextFactory.CreateSqlite();
            using var _ = context;
            using var __ = connection;
            var eventRepository = new Mock<IEventRepository>();
            eventRepository
                .Setup(r => r.GetAsync(1, false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Event { DeckName = "Sample Deck", ID = 1 });
            eventRepository
                .Setup(r => r.SetDeckAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("link failed"));
            var service = new DeckService(CardData().Object, new DeckRepository(context), eventRepository.Object, new UnitOfWork(context));

            await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                () => service.ImportAsync(new DeckImportRequest { EventID = 1, Text = Ydk(100, 200) }));

            context.ChangeTracker.Clear();
            Assert.AreEqual(0, await context.Decks.CountAsync());
            Assert.AreEqual(0, await context.DeckCards.CountAsync());
        }

        [TestMethod]
        public async Task ImportAsync_LinkOthersOff_LinksOnlyTheEvent()
        {
            using var context = InMemoryDbContextFactory.Create();
            var service = CreateService(context);
            var eventID = await AddEventAsync(context, SHARED_URL);
            var peerID = await AddEventAsync(context, SHARED_URL);

            var result = await service.ImportAsync(new DeckImportRequest { EventID = eventID, Text = Ydk(100) });

            Assert.AreEqual(1, result.LinkedEventCount);
            Assert.IsNull((await context.Events.SingleAsync(e => e.ID == peerID)).DeckID);
        }

        [TestMethod]
        public async Task ImportAsync_LinkOthersOn_LinksOnlyEventsWithTheSameURLAndNoDeck()
        {
            using var context = InMemoryDbContextFactory.Create();
            var service = CreateService(context);
            var eventID = await AddEventAsync(context, SHARED_URL);
            var peerID = await AddEventAsync(context, SHARED_URL);
            var alreadyLinkedID = await AddEventAsync(context, SHARED_URL, deckID: 77);
            var otherURLID = await AddEventAsync(context, "https://decks.example.test/other");
            var noURLID = await AddEventAsync(context);

            var result = await service.ImportAsync(new DeckImportRequest { EventID = eventID, LinkOtherEventsWithSameURL = true, Text = Ydk(100) });

            Assert.AreEqual(2, result.LinkedEventCount);
            Assert.AreEqual(result.DeckID, (await context.Events.SingleAsync(e => e.ID == eventID)).DeckID);
            Assert.AreEqual(result.DeckID, (await context.Events.SingleAsync(e => e.ID == peerID)).DeckID);
            Assert.AreEqual(77, (await context.Events.SingleAsync(e => e.ID == alreadyLinkedID)).DeckID);
            Assert.IsNull((await context.Events.SingleAsync(e => e.ID == otherURLID)).DeckID);
            Assert.IsNull((await context.Events.SingleAsync(e => e.ID == noURLID)).DeckID);
        }

        [TestMethod]
        public async Task ImportAsync_NameBlankAndEventGiven_UsesTheEventsDeckName()
        {
            using var context = InMemoryDbContextFactory.Create();
            var service = CreateService(context);
            var eventID = await AddEventAsync(context, deckName: "Event Deck Name");

            await service.ImportAsync(new DeckImportRequest { EventID = eventID, Name = "  ", Text = Ydk(100) });

            Assert.AreEqual("Event Deck Name", (await context.Decks.SingleAsync()).Name);
        }

        [TestMethod]
        public async Task ImportAsync_NameBlankAndNoEvent_ReturnsNameRequired()
        {
            using var context = InMemoryDbContextFactory.Create();
            var service = CreateService(context);

            var result = await service.ImportAsync(new DeckImportRequest { Text = Ydk(100) });

            Assert.IsFalse(result.Succeeded);
            CollectionAssert.Contains(result.Errors.ToArray(), "Deck name is required.");
            Assert.AreEqual(0, await context.Decks.CountAsync());
        }

        [TestMethod]
        public async Task ImportAsync_NullRequest_ThrowsArgumentNullException()
        {
            using var context = InMemoryDbContextFactory.Create();
            var service = CreateService(context);

            await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => service.ImportAsync(null!));
        }

        [TestMethod]
        public async Task ImportAsync_SampleYdk_StoresTheSameDeckAsSampleYdke()
        {
            using var context = InMemoryDbContextFactory.Create();
            var service = CreateService(context);

            var fromYdke = await service.ImportAsync(new DeckImportRequest { Name = "From YDKe", Text = ReadFixture("sample.ydke.txt") });
            var fromYdk = await service.ImportAsync(new DeckImportRequest { Name = "From YDK", Text = ReadFixture("sample.ydk") });

            Assert.AreEqual(Describe(context, fromYdke.DeckID), Describe(context, fromYdk.DeckID));
        }

        [TestMethod]
        public async Task ImportAsync_SampleYdke_StoresSixtyFifteenFifteenAndLinksTheEvent()
        {
            using var context = InMemoryDbContextFactory.Create();
            var service = CreateService(context);
            var eventID = await AddEventAsync(context);

            var result = await service.ImportAsync(new DeckImportRequest { EventID = eventID, Text = ReadFixture("sample.ydke.txt") });

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(60, result.MainCount);
            Assert.AreEqual(15, result.ExtraCount);
            Assert.AreEqual(15, result.SideCount);
            Assert.AreEqual(1, result.LinkedEventCount);
            Assert.AreEqual(0, result.UnknownPasscodes.Count);
            Assert.AreEqual(result.DeckID, (await context.Events.SingleAsync()).DeckID);
        }

        [TestMethod]
        public async Task ImportAsync_UnknownPasscode_IsStoredAndReported()
        {
            using var context = InMemoryDbContextFactory.Create();
            var service = CreateService(context, unknownCardIDs: [424242]);

            var result = await service.ImportAsync(new DeckImportRequest { Name = "Sample", Text = Ydk(100, 424242) });

            CollectionAssert.AreEqual(new[] { 424242 }, result.UnknownPasscodes.ToArray());
            Assert.AreEqual(2, await context.DeckCards.CountAsync());
        }

        [TestMethod]
        public async Task LinkEventAsync_DeckMissing_ReturnsZero()
        {
            using var context = InMemoryDbContextFactory.Create();
            var service = CreateService(context);
            var eventID = await AddEventAsync(context);

            Assert.AreEqual(0, await service.LinkEventAsync(eventID, 99));
        }

        [TestMethod]
        public async Task LinkEventAsync_EventMissing_ReturnsZero()
        {
            using var context = InMemoryDbContextFactory.Create();
            var service = CreateService(context);
            var deckID = (await service.ImportAsync(new DeckImportRequest { Name = "Sample", Text = Ydk(100) })).DeckID;

            Assert.AreEqual(0, await service.LinkEventAsync(99, deckID));
        }

        [TestMethod]
        public async Task LinkEventAsync_ExistingDeckAndEvent_ChangesTheEventsDeck()
        {
            using var context = InMemoryDbContextFactory.Create();
            var service = CreateService(context);
            var deckID = (await service.ImportAsync(new DeckImportRequest { Name = "Sample", Text = Ydk(100) })).DeckID;
            var eventID = await AddEventAsync(context, deckID: 77);

            var linked = await service.LinkEventAsync(eventID, deckID);

            Assert.AreEqual(1, linked);
            Assert.AreEqual(deckID, (await context.Events.SingleAsync()).DeckID);
        }

        [TestMethod]
        public async Task LinkEventAsync_LinkOthersOn_LinksTheEventAndItsUnlinkedURLPeers()
        {
            using var context = InMemoryDbContextFactory.Create();
            var service = CreateService(context);
            var deckID = (await service.ImportAsync(new DeckImportRequest { Name = "Sample", Text = Ydk(100) })).DeckID;
            var eventID = await AddEventAsync(context, SHARED_URL);
            var peerID = await AddEventAsync(context, SHARED_URL);

            var linked = await service.LinkEventAsync(eventID, deckID, linkOtherEventsWithSameURL: true);

            Assert.AreEqual(2, linked);
            Assert.AreEqual(deckID, (await context.Events.SingleAsync(e => e.ID == peerID)).DeckID);
        }
        [TestMethod]
        public void Preview_InvalidText_ReturnsFailure()
        {
            using var context = InMemoryDbContextFactory.Create();
            var service = CreateService(context);

            var result = service.Preview("nonsense");

            Assert.IsFalse(result.Succeeded);
        }

        [TestMethod]
        public async Task Preview_ValidText_ReturnsCountsAndStoresNothing()
        {
            using var context = InMemoryDbContextFactory.Create();
            var service = CreateService(context, unknownCardIDs: [200]);

            var result = service.Preview("#main\n100\n200\n#extra\n900\n");

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(2, result.MainCount);
            Assert.AreEqual(1, result.ExtraCount);
            Assert.AreEqual(0, result.SideCount);
            CollectionAssert.AreEqual(new[] { 200 }, result.UnknownPasscodes.ToArray());
            Assert.AreEqual(0, await context.Decks.CountAsync());
        }

        [TestMethod]
        public async Task UnlinkEventAsync_EventExists_ClearsItsDeck()
        {
            using var context = InMemoryDbContextFactory.Create();
            var service = CreateService(context);
            var eventID = await AddEventAsync(context, deckID: 5);

            var unlinked = await service.UnlinkEventAsync(eventID);

            Assert.IsTrue(unlinked);
            Assert.IsNull((await context.Events.SingleAsync()).DeckID);
        }

        [TestMethod]
        public async Task UnlinkEventAsync_EventMissing_ReturnsFalse()
        {
            using var context = InMemoryDbContextFactory.Create();
            var service = CreateService(context);

            Assert.IsFalse(await service.UnlinkEventAsync(99));
        }

        [TestMethod]
        public async Task UpdateAsync_BlankName_ReturnsFailure()
        {
            using var context = InMemoryDbContextFactory.Create();
            var service = CreateService(context);
            var deckID = (await service.ImportAsync(new DeckImportRequest { Name = "Sample", Text = Ydk(100) })).DeckID;

            var result = await service.UpdateAsync(deckID, "  ", null);

            Assert.IsFalse(result.Succeeded);
            Assert.IsFalse(result.NotFound);
            Assert.AreEqual("Sample", (await context.Decks.SingleAsync()).Name);
        }

        [TestMethod]
        public async Task UpdateAsync_DeckMissing_ReturnsNotFound()
        {
            using var context = InMemoryDbContextFactory.Create();
            var service = CreateService(context);

            var result = await service.UpdateAsync(99, "Name", null);

            Assert.IsTrue(result.NotFound);
        }

        [TestMethod]
        public async Task UpdateAsync_ValidValues_SavesTrimmedNameAndNotes()
        {
            using var context = InMemoryDbContextFactory.Create();
            var service = CreateService(context);
            var deckID = (await service.ImportAsync(new DeckImportRequest { Name = "Sample", Text = Ydk(100) })).DeckID;

            var result = await service.UpdateAsync(deckID, "  Renamed ", "  a note ");

            Assert.IsTrue(result.Succeeded);
            var deck = await context.Decks.SingleAsync();
            Assert.AreEqual("Renamed", deck.Name);
            Assert.AreEqual("a note", deck.Notes);
        }

        private static async Task<int> AddEventAsync(AppDBContext context, string? decklistURL = null, int? deckID = null, string deckName = "Sample Deck")
        {
            var tournamentEvent = new Event
            {
                Date = new DateOnly(2024, 5, 4),
                DateCreated = DateTime.UtcNow,
                DateModified = DateTime.UtcNow,
                DeckID = deckID,
                DeckName = deckName,
                DecklistURL = decklistURL,
                Location = "Test Hobby Shop"
            };
            context.Events.Add(tournamentEvent);
            await context.SaveChangesAsync();
            return tournamentEvent.ID;
        }

        private static Mock<ICardDataRepository> CardData(IReadOnlyDictionary<int, int>? aliases = null, int[]? unknownCardIDs = null, IReadOnlyDictionary<int, string>? names = null)
        {
            var unknown = unknownCardIDs ?? [];
            var cardData = new Mock<ICardDataRepository>();
            cardData.Setup(r => r.GetPasscodeAliases()).Returns(aliases ?? new Dictionary<int, int>());
            cardData
                .Setup(r => r.GetCardByID(It.IsAny<int>()))
                .Returns((int id) => unknown.Contains(id)
                    ? null
                    : new Card { CardType = "Effect Monster", ID = id, Name = names?.GetValueOrDefault(id) ?? $"Card {id}" });
            return cardData;
        }

        private static DeckService CreateService(AppDBContext context, IReadOnlyDictionary<int, int>? aliases = null, int[]? unknownCardIDs = null, IReadOnlyDictionary<int, string>? names = null) =>
            new(CardData(aliases, unknownCardIDs, names).Object, new DeckRepository(context), new EventRepository(context), new UnitOfWork(context));

        // The stored rows in a form that two decks can be compared by.
        private static string Describe(AppDBContext context, int deckID) =>
            string.Join(
                ";",
                context.DeckCards
                    .Where(c => c.DeckID == deckID)
                    .OrderBy(c => c.Section)
                    .ThenBy(c => c.SortOrder)
                    .AsEnumerable()
                    .Select(c => $"{c.Section}:{c.CardID}x{c.Quantity}@{c.SortOrder}"));

        private static string ReadFixture(string fileName) =>
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "Decks", fileName));

        private static string Ydk(params int[] main) =>
            "#main\n" + string.Join("\n", main);
    }
}
