using CardCollector.Data;
using CardCollector.Data.Models.Catalog;
using CardCollector.Models.Catalog;
using CardCollector.Models.Legacy;
using CardCollector.Repository.Catalog;
using CardCollector.Repository.Legacy;
using CardCollector.Services;
using CardCollector.Tests.TestHelpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CardCollector.Tests.Services
{
    /// <summary>
    /// Migrates a real legacy SQLite file (ATTACH and the read-only reader need a file) into an in-memory database whose
    /// catalog is seeded directly, with the catalog sync itself mocked.
    /// </summary>
    [TestClass]
    public sealed class LegacyMigrationServiceTests
    {
        private SqliteConnection _connection = null!;
        private string _legacyPath = null!;

        [TestCleanup]
        public void Cleanup()
        {
            _connection.Dispose();
            SqliteConnection.ClearAllPools();
            File.Delete(_legacyPath);
        }

        [TestInitialize]
        public void Initialize()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            _legacyPath = Path.Combine(Path.GetTempPath(), $"legacy-{Guid.NewGuid():N}.db");

            using var legacy = new SqliteConnection($"Data Source={_legacyPath};Pooling=False");
            legacy.Open();
            using var command = legacy.CreateCommand();
            command.CommandText = """
                CREATE TABLE "CollectionEntries" ("ID" INTEGER PRIMARY KEY, "CardID" INTEGER NOT NULL, "SetCode" TEXT NOT NULL, "Status" TEXT NOT NULL,
                    "Condition" TEXT NULL, "Edition" TEXT NULL, "PurchaseDate" TEXT NULL, "PurchasePrice" REAL NULL, "DateCreated" TEXT NOT NULL,
                    "DateModified" TEXT NOT NULL, "AcquisitionMethod" TEXT NULL, "IsPlaceholder" INTEGER NOT NULL DEFAULT 0, "Quantity" INTEGER NOT NULL,
                    "MarketPriceAtEntry" REAL NULL, "RarityName" TEXT NULL, "PrintVariant" TEXT NULL);
                INSERT INTO "CollectionEntries" VALUES
                    (2097, 48739627, 'BLGG-EN127', 'Owned', 'NearMint', 'FirstEdition', '2026-06-04 00:00:00', NULL, '2026-06-13 19:05:20.7397705', '2026-06-13 19:05:20.7397706', 'Pulled', 0, 3, 0.18, 'Ultra Rare', NULL),
                    (5000, 11111111, 'GONE-EN001', 'Owned', 'NearMint', 'Unlimited', NULL, NULL, '2026-06-13 19:05:20.7397705', '2026-06-13 19:05:20.7397706', NULL, 0, 1, NULL, 'Rare', NULL);
                CREATE TABLE "CollectionValueSnapshots" ("ID" INTEGER PRIMARY KEY, "CardCount" INTEGER NOT NULL, "DateCreated" TEXT NOT NULL,
                    "SnapshotDate" TEXT NOT NULL, "TotalValue" REAL NOT NULL);
                INSERT INTO "CollectionValueSnapshots" VALUES (1, 14176, '2026-10-12 04:11:00', '2026-10-12', 33852.44);
                CREATE TABLE "CollectionEntryValueSnapshots" ("ID" INTEGER PRIMARY KEY, "CollectionEntryID" INTEGER NOT NULL, "CardName" TEXT NOT NULL,
                    "SetCode" TEXT NOT NULL, "SetName" TEXT NOT NULL, "RarityName" TEXT NOT NULL, "Edition" TEXT NULL, "MarketValue" REAL NOT NULL,
                    "SnapshotDate" TEXT NOT NULL, "DateCreated" TEXT NOT NULL);
                INSERT INTO "CollectionEntryValueSnapshots" VALUES
                    (1, 2097, 'Synthetic Theory (Old Name)', 'BLGG-EN127', 'Synthetic Set', 'Ultra Rare', 'FirstEdition', 0.54, '2026-10-12', '2026-10-12 04:11:00'),
                    (2, 4000, 'Synthetic Theory', 'BLGG-EN127', 'Synthetic Set', 'Ultra Rare', NULL, 0.20, '2026-09-12', '2026-09-12 04:11:00'),
                    (3, 4001, 'Synthetic Card Nobody Has', 'NONE-EN001', 'Synthetic Set', 'Rare', NULL, 1.00, '2026-09-12', '2026-09-12 04:11:00');
                """;
            command.ExecuteNonQuery();
        }

        [TestMethod]
        public async Task MigrateAsync_CatalogSyncFails_Throws()
        {
            using var context = NewContext();
            var service = CreateService(context, CatalogSyncRunStatus.Failed);

            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => service.MigrateAsync(_legacyPath, CatalogSourceMode.CacheOnly));
        }

        [TestMethod]
        public async Task MigrateAsync_LegacyRows_AreMappedCopiedAndReported()
        {
            SeedCatalog();

            using var context = NewContext();
            var report = await CreateService(context, CatalogSyncRunStatus.Succeeded).MigrateAsync(_legacyPath, CatalogSourceMode.CacheOnly);

            using var verify = NewContext();
            var entry = await verify.CollectionEntries.SingleAsync();
            var card = await verify.CatalogCards.SingleAsync();
            Assert.AreEqual(2097, entry.ID);
            Assert.AreEqual(card.ID, (await verify.CatalogPrintings.SingleAsync(p => p.ID == entry.CatalogPrintingID)).CatalogCardID);
            Assert.AreEqual(33852.44m, (await verify.CollectionValueSnapshots.SingleAsync()).TotalValue);
            var item = await verify.CatalogReviewItems.SingleAsync();
            Assert.AreEqual(CatalogReviewItemKind.LegacyRowUnmapped, item.Kind);
            Assert.AreEqual("legacy:CollectionEntries:5000", item.SubjectKey);
            StringAssert.Contains(report.ToText(), "Every legacy row is accounted for");
        }

        [TestMethod]
        public async Task MigrateAsync_ValueSnapshots_AreLinkedToCardsThroughTheirEntryThenByName()
        {
            SeedCatalog();

            using var context = NewContext();
            var report = await CreateService(context, CatalogSyncRunStatus.Succeeded).MigrateAsync(_legacyPath, CatalogSourceMode.CacheOnly);

            using var verify = NewContext();
            var cardID = (await verify.CatalogCards.SingleAsync()).ID;
            var snapshots = await verify.CollectionEntryValueSnapshots.OrderBy(s => s.ID).ToListAsync();
            Assert.AreEqual(cardID, snapshots[0].CatalogCardID);
            Assert.AreEqual(cardID, snapshots[1].CatalogCardID);
            Assert.IsNull(snapshots[2].CatalogCardID);
            Assert.AreEqual(new LegacySnapshotCardFill(1, 1, 1), report.SnapshotCardFill);
            StringAssert.Contains(report.ToText(), "1 through their entry, 1 by card name, 1 not linked");
        }

        private LegacyMigrationService CreateService(AppDBContext context, CatalogSyncRunStatus syncStatus)
        {
            var sync = new Mock<ICatalogSyncService>();
            sync.Setup(s => s.SyncAsync(It.IsAny<CatalogSourceMode>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CatalogSyncResult { Run = new CatalogSyncRun { ID = 1, Status = syncStatus } });

            return new LegacyMigrationService(
                new CatalogRepository(context),
                sync.Object,
                new LegacyImportRepository(context),
                new LegacyDatabaseReader(),
                NullLogger<LegacyMigrationService>.Instance,
                new FakeTimeProvider(new DateTimeOffset(2026, 10, 12, 4, 0, 0, TimeSpan.Zero)));
        }

        private AppDBContext NewContext() =>
            new(new DbContextOptionsBuilder<AppDBContext>().UseSqlite(_connection).Options);

        /// <summary>Seeds one synthetic card with the printing legacy entry 2097 maps to.</summary>
        private void SeedCatalog()
        {
            using var seedContext = NewContext();
            seedContext.Database.EnsureCreated();
            var theory = CatalogTestData.Card(0, "Synthetic Theory", 21466, 74733322,
                (CatalogSource.YamlYugi, CatalogAliasKind.Passcode, 74733322), (CatalogSource.YGOProDeck, CatalogAliasKind.YGOProDeckID, 48739627));
            var printing = CatalogTestData.Printing(theory, 0, "BLGG-EN127", "Ultra Rare");
            printing.CatalogCardID = 0;
            seedContext.CatalogCards.Add(theory);
            seedContext.SaveChanges();
        }
    }
}
