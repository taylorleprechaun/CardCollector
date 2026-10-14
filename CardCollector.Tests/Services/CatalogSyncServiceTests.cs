using CardCollector.Data;
using CardCollector.Data.Models.Catalog;
using CardCollector.DTO;
using CardCollector.DTO.YamlYugi;
using CardCollector.DTO.YGOProDeck;
using CardCollector.Models.Catalog;
using CardCollector.Repository;
using CardCollector.Repository.Catalog;
using CardCollector.Repository.Sources;
using CardCollector.Services;
using CardCollector.Tests.TestHelpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace CardCollector.Tests.Services
{
    /// <summary>
    /// Runs whole syncs against a real in-memory SQLite database (transactions and unique indexes matter here), with each
    /// run in a fresh context the way each nightly run gets its own DI scope.
    /// </summary>
    [TestClass]
    public sealed class CatalogSyncServiceTests
    {
        private SqliteConnection _connection = null!;
        private int _runCount;
        private IList<TCGPriceSet> _tcgRows = null!;
        private IList<YamlCard> _yamlCards = null!;
        private IList<YGOProDeckCard> _ygoProDeckCards = null!;

        [TestCleanup]
        public void Cleanup() => _connection.Dispose();

        [TestMethod]
        [DataRow(0, null, CatalogSourceStatus.Failed, DisplayName = "Nothing downloaded")]
        [DataRow(100, null, CatalogSourceStatus.Succeeded, DisplayName = "First run")]
        [DataRow(95, 100, CatalogSourceStatus.Succeeded, DisplayName = "Small drop")]
        [DataRow(50, 100, CatalogSourceStatus.Skipped, DisplayName = "Partial download")]
        public void EvaluateSource_ReturnsExpectedStatus(int count, int? lastSucceededCount, CatalogSourceStatus expected)
        {
            var status = CatalogSyncService.EvaluateSource(count, lastSucceededCount, 0.9);

            Assert.AreEqual(expected, status);
        }

        [TestInitialize]
        public void Initialize()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using (var context = NewContext())
                context.Database.EnsureCreated();

            _yamlCards =
            [
                YamlCard(21466, 48739627, "Synthetic Theory", ("SYN-EN055", "Common")),
                YamlCard(21465, 74733322, "Synthetic Academy", ("SYN-EN054", "Secret Rare"))
            ];
            _ygoProDeckCards =
            [
                YGOProDeckCard(48739627, 21466, "Synthetic Theory"),
                YGOProDeckCard(74733322, 21465, "Synthetic Academy")
            ];
            _tcgRows =
            [
                new() { CardName = "Synthetic Theory", Code = "SYN-EN055", Edition = "1st Edition", ProductID = 900, RarityName = "Common" },
                new() { CardName = "Synthetic Academy", Code = "SYN-EN054", Edition = "1st Edition", ProductID = 901, RarityName = "Secret Rare" }
            ];
        }

        [TestMethod]
        public async Task SyncAsync_BootstrapWithoutYGOProDeck_FailsAndBuildsNothing()
        {
            _ygoProDeckCards.Clear();

            var result = await RunSyncAsync();

            Assert.AreEqual(CatalogSyncRunStatus.Failed, result.Run.Status);
            using var context = NewContext();
            Assert.AreEqual(0, await context.CatalogCards.CountAsync());
        }

        [TestMethod]
        public async Task SyncAsync_EmptyCatalog_BootstrapsThenSecondRunChangesNothing()
        {
            var first = await RunSyncAsync();
            var second = await RunSyncAsync();

            Assert.AreEqual(CatalogSyncRunStatus.Succeeded, first.Run.Status);
            Assert.IsTrue(first.Run.IsBootstrap);
            Assert.AreEqual(2, first.Run.CardsAdded);
            Assert.AreEqual(2, first.Run.ProductsLinked);
            Assert.AreEqual(CatalogSyncRunStatus.Succeeded, second.Run.Status);
            Assert.AreEqual(0, second.Run.CardsAdded + second.Run.CardsUpdated + second.Run.PrintingsAdded + second.Run.ItemsOpened);
            using var context = NewContext();
            Assert.AreEqual(2, await context.CatalogCards.CountAsync());
            Assert.AreEqual(2, await context.CatalogPrintingSources.CountAsync());
        }

        [TestMethod]
        public async Task SyncAsync_PasscodesSwappedUpstream_KeepsCardsAndOpensPasscodeItems()
        {
            await RunSyncAsync();
            _yamlCards =
            [
                YamlCard(21466, 74733322, "Synthetic Theory", ("SYN-EN055", "Common")),
                YamlCard(21465, 48739627, "Synthetic Academy", ("SYN-EN054", "Secret Rare"))
            ];

            var result = await RunSyncAsync();

            Assert.AreEqual(2, result.ItemsOpenedByKind[CatalogReviewItemKind.PasscodeChanged]);
            using var context = NewContext();
            var theory = await context.CatalogCards.Include(c => c.Printings).SingleAsync(c => c.KonamiID == 21466);
            Assert.AreEqual("Synthetic Theory", theory.Name);
            Assert.AreEqual(48739627, theory.Passcode);
            Assert.AreEqual("SYN-EN055", theory.Printings.Single().SetCode);
        }

        [TestMethod]
        public async Task SyncAsync_SourceShrank_SkipsSourceAndReportsNoMissingCards()
        {
            await RunSyncAsync();
            _yamlCards.RemoveAt(1);

            var result = await RunSyncAsync();

            Assert.AreEqual(CatalogSourceStatus.Skipped, result.Run.YamlYugiStatus);
            Assert.IsFalse(result.ItemsOpenedByKind.ContainsKey(CatalogReviewItemKind.CardNotSeen));
        }

        [TestMethod]
        public async Task SyncAsync_StartUpWithCacheWrittenAfterLastRun_Syncs()
        {
            await RunSyncAsync();

            var result = await RunSyncAsync(sourceMode: CatalogSourceMode.IfStale, cachedAtUTC: new DateTime(2026, 10, 12, 5, 0, 0, DateTimeKind.Utc));

            Assert.IsFalse(result.Skipped);
            using var context = NewContext();
            Assert.AreEqual(2, await context.CatalogSyncRuns.CountAsync());
        }

        [TestMethod]
        public async Task SyncAsync_StartUpWithSourcesUnchangedSinceLastRun_SkipsWithoutRecordingARun()
        {
            var first = await RunSyncAsync();

            var result = await RunSyncAsync(sourceMode: CatalogSourceMode.IfStale, cachedAtUTC: new DateTime(2026, 10, 11, 23, 0, 0, DateTimeKind.Utc));

            Assert.IsTrue(result.Skipped);
            Assert.AreEqual(first.Run.ID, result.Run.ID);
            using var context = NewContext();
            Assert.AreEqual(1, await context.CatalogSyncRuns.CountAsync());
        }

        [TestMethod]
        public async Task SyncAsync_TooManyActionableItems_RollsBackAndRecordsAnomaly()
        {
            await RunSyncAsync();
            _yamlCards =
            [
                YamlCard(21466, 48739627, "Synthetic Theory Renamed", ("SYN-EN055", "Common")),
                YamlCard(21465, 74733322, "Synthetic Academy", ("SYN-EN054", "Secret Rare"), ("SYN-EN054", "Starlight Rare"))
            ];

            var result = await RunSyncAsync(maxNewActionableItems: 0);

            Assert.AreEqual(CatalogSyncRunStatus.Aborted, result.Run.Status);
            using var context = NewContext();
            Assert.AreEqual(2, await context.CatalogPrintings.CountAsync());
            Assert.AreEqual(CatalogReviewItemKind.SyncAnomaly, (await context.CatalogReviewItems.SingleAsync()).Kind);
            Assert.AreEqual(CatalogSyncRunStatus.Aborted, (await context.CatalogSyncRuns.OrderBy(r => r.ID).LastAsync()).Status);
        }

        private static YamlCard YamlCard(int konamiID, int passcode, string name, params (string SetCode, string Rarity)[] printings) => new()
        {
            CardType = "Spell",
            KonamiID = konamiID,
            Name = new YamlLocalizedString { En = name },
            Password = passcode,
            Sets = new YamlCardSets
            {
                En = printings
                    .GroupBy(p => p.SetCode)
                    .Select(g => new YamlSetEntry { Rarities = g.Select(p => p.Rarity).ToList(), SetName = "Synthetic Set", SetNumber = g.Key })
                    .ToList()
            }
        };

        private static YGOProDeckCard YGOProDeckCard(int id, int konamiID, string name) => new()
        {
            CardImages = [new Image { ID = id }],
            ID = id,
            KonamiID = konamiID,
            MiscInfo = [new YGOProDeckMiscInfo { KonamiID = konamiID }],
            Name = name
        };

        private AppDBContext NewContext() =>
            new(new DbContextOptionsBuilder<AppDBContext>().UseSqlite(_connection).Options);

        /// <param name="maxNewActionableItems">The guardrail on newly opened actionable items.</param>
        /// <param name="sourceMode">How the run reads its sources; the start-up mode reads <paramref name="cachedAtUTC"/>.</param>
        /// <param name="cachedAtUTC">When every source's cache file was written, as the sources report it.</param>
        private async Task<CatalogSyncResult> RunSyncAsync(
            int maxNewActionableItems = 250,
            CatalogSourceMode sourceMode = CatalogSourceMode.CacheOnly,
            DateTime? cachedAtUTC = null)
        {
            var yamlSource = new Mock<IYamlYugiSource>();
            yamlSource.Setup(s => s.ReadCached()).Returns(_yamlCards.ToList());
            yamlSource.Setup(s => s.LoadAsync(false)).ReturnsAsync(_yamlCards.ToList());
            yamlSource.Setup(s => s.CachedAtUTC).Returns(cachedAtUTC);
            var ygoProDeckSource = new Mock<IYGOProDeckSource>();
            ygoProDeckSource.Setup(s => s.ReadCached()).Returns(_ygoProDeckCards.ToList());
            ygoProDeckSource.Setup(s => s.LoadAsync(false)).ReturnsAsync(_ygoProDeckCards.ToList());
            ygoProDeckSource.Setup(s => s.CachedAtUTC).Returns(cachedAtUTC);
            var tcgCatalogCache = new Mock<ITCGCatalogCache>();
            tcgCatalogCache.Setup(c => c.GetAllPrintings()).Returns(_tcgRows.ToList());
            tcgCatalogCache.Setup(c => c.CachedAtUTC).Returns(cachedAtUTC);

            using var context = NewContext();
            var service = new CatalogSyncService(
                new Mock<ICardSetRepository>().Object,
                new CatalogRepository(context),
                NullLogger<CatalogSyncService>.Instance,
                Options.Create(new CatalogSyncSettings { MaxNewActionableItems = maxNewActionableItems }),
                tcgCatalogCache.Object,
                new UnitOfWork(context),
                yamlSource.Object,
                ygoProDeckSource.Object,
                new FakeTimeProvider(new DateTimeOffset(2026, 10, 12, 4, 0, 0, TimeSpan.Zero).AddDays(_runCount++)));

            return await service.SyncAsync(sourceMode);
        }
    }
}
