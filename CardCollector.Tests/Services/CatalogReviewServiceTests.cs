using CardCollector.Data;
using CardCollector.Data.Models;
using CardCollector.Data.Models.Catalog;
using CardCollector.Repository;
using CardCollector.Repository.Catalog;
using CardCollector.Services;
using CardCollector.Tests.TestHelpers;
using CardCollector.ViewModels;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CardCollector.Tests.Services
{
    /// <summary>Applies review decisions against a real in-memory SQLite database (transactions and rollbacks matter here).</summary>
    [TestClass]
    public sealed class CatalogReviewServiceTests
    {
        private Mock<ICardDataRepository> _cardDataRepositoryMock = null!;
        private int _cardID;
        private SqliteConnection _connection = null!;
        private int _printingID;

        [TestMethod]
        public async Task AcceptAsync_CardNameChanged_RenamesCardAndClosesItem()
        {
            var itemID = await AddItemAsync(CatalogReviewItemKind.CardNameChanged, """{"Current":"Synthetic Theory","Reported":"Synthetic Theorem"}""", cardID: _cardID);

            var result = await Service().AcceptAsync(itemID);

            Assert.IsTrue(result.Succeeded);
            using var verify = NewContext();
            Assert.AreEqual("Synthetic Theorem", (await verify.CatalogCards.SingleAsync()).Name);
            var item = await verify.CatalogReviewItems.SingleAsync();
            Assert.AreEqual(CatalogReviewItemStatus.Accepted, item.Status);
            Assert.AreEqual("Renamed from \"Synthetic Theory\" to \"Synthetic Theorem\"", item.Resolution);
        }

        [TestMethod]
        public async Task AcceptAsync_CardNotSeenAndUnused_RetiresTheCard()
        {
            var itemID = await AddItemAsync(CatalogReviewItemKind.CardNotSeen, "{}", cardID: _cardID);

            var result = await Service().AcceptAsync(itemID);

            Assert.IsTrue(result.Succeeded);
            using var verify = NewContext();
            Assert.AreEqual(CatalogStatus.Retired, (await verify.CatalogCards.SingleAsync()).Status);
        }

        [TestMethod]
        public async Task AcceptAsync_CardNotSeenButInTheCollection_FailsAndKeepsTheCard()
        {
            await AddCollectionEntryAsync();
            var itemID = await AddItemAsync(CatalogReviewItemKind.CardNotSeen, "{}", cardID: _cardID);

            var result = await Service().AcceptAsync(itemID);

            Assert.IsFalse(result.Succeeded);
            using var verify = NewContext();
            Assert.AreEqual(CatalogStatus.Active, (await verify.CatalogCards.SingleAsync()).Status);
            Assert.AreEqual(CatalogReviewItemStatus.Open, (await verify.CatalogReviewItems.SingleAsync()).Status);
        }

        [TestMethod]
        public async Task AcceptAsync_InformationalItem_IsRefused()
        {
            var itemID = await AddItemAsync(CatalogReviewItemKind.CardAdded, "{}", cardID: _cardID);

            var result = await Service().AcceptAsync(itemID);

            Assert.IsFalse(result.Succeeded);
            _cardDataRepositoryMock.Verify(r => r.QueueReload(), Times.Never);
        }

        [TestMethod]
        public async Task AcceptAsync_ItemAlreadyDecided_ReportsItIsNotOpen()
        {
            var itemID = await AddItemAsync(CatalogReviewItemKind.PasscodeChanged, "{}", cardID: _cardID, status: CatalogReviewItemStatus.Rejected);

            var result = await Service().AcceptAsync(itemID);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual("That review item is not open.", result.Errors.Single());
        }

        [TestMethod]
        public async Task AcceptAsync_PasscodeChanged_UpdatesPasscodeAndClosesItem()
        {
            var itemID = await AddItemAsync(CatalogReviewItemKind.PasscodeChanged, """{"CardName":"Synthetic Theory","Current":48739627,"Reported":74733322}""", cardID: _cardID);

            var result = await Service().AcceptAsync(itemID);

            Assert.IsTrue(result.Succeeded);
            using var verify = NewContext();
            Assert.AreEqual(74733322, (await verify.CatalogCards.SingleAsync()).Passcode);
            Assert.AreEqual(CatalogReviewItemStatus.Accepted, (await verify.CatalogReviewItems.SingleAsync()).Status);
            _cardDataRepositoryMock.Verify(r => r.QueueReload(), Times.Once);
        }

        [TestMethod]
        public async Task AcceptAsync_PrintingNotSeenAndUnused_RetiresThePrinting()
        {
            var itemID = await AddItemAsync(CatalogReviewItemKind.PrintingNotSeen, "{}", printingID: _printingID);

            var result = await Service().AcceptAsync(itemID);

            Assert.IsTrue(result.Succeeded);
            using var verify = NewContext();
            Assert.AreEqual(CatalogStatus.Retired, (await verify.CatalogPrintings.SingleAsync()).Status);
            Assert.AreEqual("Printing BLGG-EN127 Ultra Rare retired", (await verify.CatalogReviewItems.SingleAsync()).Resolution);
        }

        [TestMethod]
        public async Task AcceptAsync_PrintingStillInCollection_FailsAndLeavesItOpen()
        {
            await AddCollectionEntryAsync();
            var itemID = await AddItemAsync(CatalogReviewItemKind.PrintingNotSeen, "{}", printingID: _printingID);

            var result = await Service().AcceptAsync(itemID);

            Assert.IsFalse(result.Succeeded);
            using var verify = NewContext();
            Assert.AreEqual(CatalogStatus.Active, (await verify.CatalogPrintings.SingleAsync()).Status);
            Assert.AreEqual(CatalogReviewItemStatus.Open, (await verify.CatalogReviewItems.SingleAsync()).Status);
        }

        [TestMethod]
        public async Task AcceptAsync_PrintingTrackedOnTheWishlist_FailsAndLeavesItOpen()
        {
            using (var context = NewContext())
            {
                context.PreferredVersions.Add(new PreferredVersion { CatalogPrintingID = _printingID });
                await context.SaveChangesAsync();
            }

            var itemID = await AddItemAsync(CatalogReviewItemKind.PrintingNotSeen, "{}", printingID: _printingID);

            var result = await Service().AcceptAsync(itemID);

            Assert.IsFalse(result.Succeeded);
        }

        [TestCleanup]
        public void Cleanup() => _connection.Dispose();

        [TestMethod]
        public async Task CreateCardAsync_CardWithThatNameExists_FailsAndLeavesItOpen()
        {
            var itemID = await AddItemAsync(CatalogReviewItemKind.UnmappedTCGProduct,
                """{"ProductID":97893,"CardName":"synthetic theory","SetCode":"BLGG-EN128","RarityName":"Secret Rare"}""");

            var result = await Service().CreateCardAsync(itemID);

            Assert.IsFalse(result.Succeeded);
            using var verify = NewContext();
            Assert.AreEqual(1, await verify.CatalogCards.CountAsync());
            Assert.AreEqual(CatalogReviewItemStatus.Open, (await verify.CatalogReviewItems.SingleAsync()).Status);
        }

        [TestMethod]
        public async Task CreateCardAsync_NoCardWithThatName_CreatesCardWithLinkedPrintingAndClosesItem()
        {
            var itemID = await AddItemAsync(CatalogReviewItemKind.UnmappedTCGProduct, """{"ProductID":97893,"CardName":"Synthetic Legend","SetCode":"BLGG-EN128","RarityName":"Prismatic Secret Rare","PrintVariant":null}""");

            var result = await Service().CreateCardAsync(itemID);

            Assert.IsTrue(result.Succeeded);
            using var verify = NewContext();
            var card = await verify.CatalogCards.Include(c => c.Printings).ThenInclude(p => p.Sources).SingleAsync(c => c.Name == "Synthetic Legend");
            Assert.IsNull(card.KonamiID);
            Assert.IsNull(card.Passcode);
            var printing = card.Printings.Single();
            Assert.AreEqual("BLGG-EN128", printing.SetCode);
            Assert.AreEqual("97893", printing.Sources.Single(s => s.Source == CatalogSource.TCGPlayer).ExternalKey);
            Assert.AreEqual(1, printing.Sources.Count(s => s.Source == CatalogSource.Manual));
            var item = await verify.CatalogReviewItems.SingleAsync();
            Assert.AreEqual(CatalogReviewItemStatus.Accepted, item.Status);
            Assert.AreEqual(card.ID, item.CatalogCardID);
            _cardDataRepositoryMock.Verify(r => r.QueueReload(), Times.Once);
        }

        [TestMethod]
        public async Task CreatePrintingAsync_LegacyRowUnmapped_FailsAndLeavesItOpen()
        {
            var itemID = await AddItemAsync(CatalogReviewItemKind.LegacyRowUnmapped, """{"Table":"CollectionEntries","Row":{"ID":4331,"CardID":37440988,"SetCode":"LDS1-EN101","RarityName":"Ultra Rare","Quantity":2},"Reason":"No match"}""");

            var result = await Service().CreatePrintingAsync(itemID, _cardID);

            Assert.IsFalse(result.Succeeded);
            using var verify = NewContext();
            Assert.IsFalse(await verify.CatalogPrintings.AnyAsync(p => p.SetCode == "LDS1-EN101"));
            Assert.AreEqual(CatalogReviewItemStatus.Open, (await verify.CatalogReviewItems.SingleAsync()).Status);
        }

        [TestMethod]
        public async Task CreatePrintingAsync_MissingPrinting_CreatesManualPrintingAndLinksProduct()
        {
            var itemID = await AddItemAsync(CatalogReviewItemKind.UnmappedTCGProduct,
                """{"ProductID":79079,"CardName":"Synthetic Theory","SetCode":"LC01-EN001","RarityName":"Ultra Rare","PrintVariant":"Blue"}""");

            var result = await Service().CreatePrintingAsync(itemID, _cardID);

            Assert.IsTrue(result.Succeeded);
            using var verify = NewContext();
            var created = await verify.CatalogPrintings.Include(p => p.Sources).SingleAsync(p => p.SetCode == "LC01-EN001");
            Assert.AreEqual("Blue", created.PrintVariant);
            CollectionAssert.AreEquivalent(new[] { CatalogSource.Manual, CatalogSource.TCGPlayer }, created.Sources.Select(x => x.Source).ToList());
            Assert.AreEqual(CatalogReviewItemStatus.Accepted, (await verify.CatalogReviewItems.SingleAsync()).Status);
        }

        [TestMethod]
        public async Task CreatePrintingAsync_ProductAlreadyLinked_RollsBackTheNewPrinting()
        {
            await LinkProductAsync("79079");
            var itemID = await AddItemAsync(CatalogReviewItemKind.UnmappedTCGProduct,
                """{"ProductID":79079,"CardName":"Synthetic Theory","SetCode":"LC01-EN001","RarityName":"Ultra Rare","PrintVariant":null}""");

            var result = await Service().CreatePrintingAsync(itemID, _cardID);

            Assert.IsFalse(result.Succeeded);
            using var verify = NewContext();
            Assert.IsFalse(await verify.CatalogPrintings.AnyAsync(p => p.SetCode == "LC01-EN001"));
        }

        [TestMethod]
        public async Task DismissAllInformationalAsync_MixedItems_ClosesOnlyInformationalOnes()
        {
            await AddItemAsync(CatalogReviewItemKind.CardAdded, "{}", subject: "card:1");
            await AddItemAsync(CatalogReviewItemKind.LegacyDismissalUnmapped, "{}", subject: "legacy:DismissedNewPrintings:1");
            await AddItemAsync(CatalogReviewItemKind.PasscodeChanged, "{}", subject: "card:2");

            var dismissed = await Service().DismissAllInformationalAsync(null);

            Assert.AreEqual(2, dismissed);
            using var verify = NewContext();
            Assert.AreEqual(CatalogReviewItemStatus.Open, (await verify.CatalogReviewItems.SingleAsync(i => i.Kind == CatalogReviewItemKind.PasscodeChanged)).Status);
        }

        [TestMethod]
        public async Task DismissAsync_ActionableItem_IsRefused()
        {
            var itemID = await AddItemAsync(CatalogReviewItemKind.PasscodeChanged, "{}");

            var result = await Service().DismissAsync(itemID);

            Assert.IsFalse(result.Succeeded);
        }

        [TestMethod]
        public async Task DismissAsync_InformationalItem_ClosesIt()
        {
            var itemID = await AddItemAsync(CatalogReviewItemKind.CardAdded, "{}", cardID: _cardID);

            var result = await Service().DismissAsync(itemID);

            Assert.IsTrue(result.Succeeded);
            using var verify = NewContext();
            Assert.AreEqual(CatalogReviewItemStatus.Dismissed, (await verify.CatalogReviewItems.SingleAsync()).Status);
        }

        [TestMethod]
        public async Task DismissAsync_OpenLegacyRowUnmapped_ClosesIt()
        {
            var itemID = await AddItemAsync(CatalogReviewItemKind.LegacyRowUnmapped, """{"Table":"CollectionEntries","Row":{"ID":4331,"CardID":37440988,"SetCode":"LDS1-EN101","RarityName":"Ultra Rare","Quantity":2},"Reason":"No match"}""");

            var result = await Service().DismissAsync(itemID);

            Assert.IsTrue(result.Succeeded);
            using var verify = NewContext();
            Assert.AreEqual(CatalogReviewItemStatus.Dismissed, (await verify.CatalogReviewItems.SingleAsync()).Status);
            Assert.AreEqual(0, await verify.CollectionEntries.CountAsync());
        }

        [TestInitialize]
        public void Initialize()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            _cardDataRepositoryMock = new Mock<ICardDataRepository>();
            _cardDataRepositoryMock.Setup(r => r.GetCardsByName(It.IsAny<string>())).Returns([]);
            _cardDataRepositoryMock.Setup(r => r.GetPrintingsBySetCode(It.IsAny<string>())).Returns([]);

            using var context = NewContext();
            context.Database.EnsureCreated();
            var card = CatalogTestData.Card(0, "Synthetic Theory", 21466, 48739627);
            var printing = CatalogTestData.Printing(card, 0, "BLGG-EN127", "Ultra Rare");
            printing.CatalogCardID = 0;
            context.CatalogCards.Add(card);
            context.SaveChanges();
            _cardID = card.ID;
            _printingID = printing.ID;
        }

        [TestMethod]
        public async Task MapToPrintingAsync_LegacyRowUnmapped_FailsAndRestoresNothing()
        {
            var itemID = await AddItemAsync(CatalogReviewItemKind.LegacyRowUnmapped, """{"Table":"CollectionEntries","Row":{"ID":4331,"CardID":37440988,"SetCode":"LDS1-EN101","RarityName":"Ultra Rare","Quantity":2},"Reason":"No match"}""");

            var result = await Service().MapToPrintingAsync(itemID, _printingID);

            Assert.IsFalse(result.Succeeded);
            using var verify = NewContext();
            Assert.AreEqual(0, await verify.CollectionEntries.CountAsync());
            Assert.AreEqual(CatalogReviewItemStatus.Open, (await verify.CatalogReviewItems.SingleAsync()).Status);
        }

        [TestMethod]
        public async Task MapToPrintingAsync_UnmappedProduct_LinksProductToPrinting()
        {
            var itemID = await AddItemAsync(CatalogReviewItemKind.UnmappedTCGProduct,
                """{"ProductID":695684,"CardName":"Synthetic Theory","SetCode":"BLGG-EN127","RarityName":"Ultra Rare","PrintVariant":null}""");

            var result = await Service().MapToPrintingAsync(itemID, _printingID);

            Assert.IsTrue(result.Succeeded);
            using var verify = NewContext();
            var link = await verify.CatalogPrintingSources.SingleAsync();
            Assert.AreEqual("695684", link.ExternalKey);
            Assert.AreEqual(_printingID, link.CatalogPrintingID);
        }

        [TestMethod]
        public async Task RejectAsync_InformationalItem_IsRefused()
        {
            var itemID = await AddItemAsync(CatalogReviewItemKind.CardAdded, "{}", cardID: _cardID);

            var result = await Service().RejectAsync(itemID);

            Assert.IsFalse(result.Succeeded);
            using var verify = NewContext();
            Assert.AreEqual(CatalogReviewItemStatus.Open, (await verify.CatalogReviewItems.SingleAsync()).Status);
        }

        [TestMethod]
        public async Task RejectAsync_LegacyRowUnmapped_IsRefused()
        {
            var itemID = await AddItemAsync(CatalogReviewItemKind.LegacyRowUnmapped, """{"Table":"CollectionEntries","Row":{"ID":4331,"CardID":37440988,"SetCode":"LDS1-EN101","RarityName":"Ultra Rare","Quantity":2},"Reason":"No match"}""");

            var result = await Service().RejectAsync(itemID);

            Assert.IsFalse(result.Succeeded);
        }

        [TestMethod]
        public async Task ReopenAsync_AcceptedItem_Fails()
        {
            var itemID = await AddItemAsync(CatalogReviewItemKind.PasscodeChanged, "{}", status: CatalogReviewItemStatus.Accepted);

            var result = await Service().ReopenAsync(itemID);

            Assert.IsFalse(result.Succeeded);
        }

        [TestMethod]
        public async Task ReopenAsync_DismissedLegacyItem_Fails()
        {
            var itemID = await AddItemAsync(CatalogReviewItemKind.LegacyRowAutoMapped, "{}", status: CatalogReviewItemStatus.Dismissed);

            var result = await Service().ReopenAsync(itemID);

            Assert.IsFalse(result.Succeeded);
            using var verify = NewContext();
            Assert.AreEqual(CatalogReviewItemStatus.Dismissed, (await verify.CatalogReviewItems.SingleAsync()).Status);
        }

        [TestMethod]
        public async Task ReopenAsync_MissingItem_ReportsItNoLongerExists()
        {
            var result = await Service().ReopenAsync(999);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual("That review item no longer exists.", result.Errors.Single());
        }

        [TestMethod]
        public async Task ReopenAsync_RejectedItem_PutsItBackOpen()
        {
            var itemID = await AddItemAsync(CatalogReviewItemKind.UnmappedTCGProduct, """{"ProductID":97893,"CardName":"Synthetic Legend","SetCode":"BLGG-EN128","RarityName":"Prismatic Secret Rare","PrintVariant":null}""", status: CatalogReviewItemStatus.Rejected);

            var result = await Service().ReopenAsync(itemID);

            Assert.IsTrue(result.Succeeded);
            using var verify = NewContext();
            var item = await verify.CatalogReviewItems.SingleAsync();
            Assert.AreEqual(CatalogReviewItemStatus.Open, item.Status);
            Assert.IsNull(item.Resolution);
        }

        [TestMethod]
        public async Task SearchAsync_NeedsDecision_ReturnsOnlyOpenActionableItems()
        {
            await AddItemAsync(CatalogReviewItemKind.CardAdded, "{}", subject: "card:1");
            await AddItemAsync(CatalogReviewItemKind.PasscodeChanged, "{}", subject: "card:2");
            await AddItemAsync(CatalogReviewItemKind.CardNameChanged, "{}", subject: "card:3", status: CatalogReviewItemStatus.Rejected);

            var results = await Service().SearchAsync(new CatalogReviewSearchCriteria { Filter = CatalogReviewFilter.NeedsDecision });

            Assert.AreEqual(CatalogReviewItemKind.PasscodeChanged, results.Items.Single().Kind);
        }

        private async Task AddCollectionEntryAsync()
        {
            using var context = NewContext();
            context.CollectionEntries.Add(new CollectionEntry
            {
                CatalogPrintingID = _printingID,
                Quantity = 1,
                Status = CollectionStatus.Owned
            });
            await context.SaveChangesAsync();
        }

        private async Task<int> AddItemAsync(
            CatalogReviewItemKind kind,
            string payloadJSON,
            int? cardID = null,
            int? printingID = null,
            string subject = "subject",
            CatalogReviewItemStatus status = CatalogReviewItemStatus.Open)
        {
            using var context = NewContext();
            var item = new CatalogReviewItem
            {
                CatalogCardID = cardID,
                CatalogPrintingID = printingID,
                DetectionCount = 1,
                Kind = kind,
                PayloadHash = "hash",
                PayloadJSON = payloadJSON,
                Status = status,
                SubjectKey = subject,
                Summary = "Synthetic summary"
            };
            context.CatalogReviewItems.Add(item);
            await context.SaveChangesAsync();
            return item.ID;
        }

        private async Task LinkProductAsync(string productID)
        {
            using var context = NewContext();
            context.CatalogPrintingSources.Add(new CatalogPrintingSource { CatalogPrintingID = _printingID, ExternalKey = productID, Source = CatalogSource.TCGPlayer });
            await context.SaveChangesAsync();
        }

        private AppDBContext NewContext() =>
            new(new DbContextOptionsBuilder<AppDBContext>().UseSqlite(_connection).Options);

        private CatalogReviewService Service()
        {
            var context = NewContext();
            return new CatalogReviewService(
                _cardDataRepositoryMock.Object,
                NullLogger<CatalogReviewService>.Instance,
                new CatalogReviewRepository(context),
                new UnitOfWork(context),
                new FakeTimeProvider(new DateTimeOffset(2026, 10, 13, 4, 0, 0, TimeSpan.Zero)));
        }
    }
}
