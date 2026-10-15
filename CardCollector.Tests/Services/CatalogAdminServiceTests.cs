using CardCollector.Data;
using CardCollector.Data.Models;
using CardCollector.Data.Models.Catalog;
using CardCollector.Models.Catalog;
using CardCollector.Repository;
using CardCollector.Repository.Catalog;
using CardCollector.Services;
using CardCollector.Tests.TestHelpers;
using CardCollector.ViewModels;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace CardCollector.Tests.Services
{
    /// <summary>Applies hand edits to the catalog against a real in-memory SQLite database (transactions and key matching matter here).</summary>
    [TestClass]
    public sealed class CatalogAdminServiceTests
    {
        private Mock<ICardDataRepository> _cardDataRepositoryMock = null!;
        private int _cardID;
        private SqliteConnection _connection = null!;
        private int _printingID;

        [TestMethod]
        public async Task AddPrintingAsync_NewPrinting_CreatesItWithManualSource()
        {
            var result = await Service().AddPrintingAsync(_cardID, new CatalogPrintingInput { RarityName = "Starfoil", SetCode = " blgg-en127 " });

            Assert.IsTrue(result.Succeeded);
            using var verify = NewContext();
            var created = await verify.CatalogPrintings.Include(p => p.Sources).SingleAsync(p => p.ID != _printingID);
            Assert.AreEqual("BLGG-EN127", created.SetCode);
            Assert.AreEqual("Starfoil Rare", created.RarityName);
            Assert.AreEqual(CatalogSource.Manual, created.Sources.Single().Source);
            _cardDataRepositoryMock.Verify(r => r.QueueReload(), Times.Once);
        }

        [TestMethod]
        public async Task AddPrintingAsync_PrintingAlreadyExists_Fails()
        {
            var result = await Service().AddPrintingAsync(_cardID, new CatalogPrintingInput { RarityName = "Ultra Rare", SetCode = "BLGG-EN127" });

            Assert.IsFalse(result.Succeeded);
            using var verify = NewContext();
            Assert.AreEqual(1, await verify.CatalogPrintings.CountAsync());
        }

        [TestMethod]
        public async Task AddPrintingAsync_VariantRestatesRarity_StoresPlainPrinting()
        {
            var result = await Service().AddPrintingAsync(_cardID, new CatalogPrintingInput { PrintVariant = "Secret", RarityName = "Secret Rare", SetCode = "BLGG-EN127" });

            Assert.IsTrue(result.Succeeded);
            using var verify = NewContext();
            Assert.IsNull((await verify.CatalogPrintings.SingleAsync(p => p.RarityName == "Secret Rare")).PrintVariant);
        }

        [TestCleanup]
        public void Cleanup() => _connection.Dispose();

        [TestMethod]
        public async Task GetCardAsync_UserRowOnAPrinting_CountsItOnThatPrinting()
        {
            using (var context = NewContext())
            {
                context.PreferredVersions.Add(new PreferredVersion { CatalogPrintingID = _printingID });
                await context.SaveChangesAsync();
            }

            var card = await Service().GetCardAsync(_cardID);

            Assert.AreEqual(1, card!.Printings.Single().UserRowCount);
            Assert.AreEqual(1, card.UserRowCount);
        }

        [TestMethod]
        public async Task GetSyncHistoryAsync_SourceFailedThreeRunsInARow_ReportsStreak()
        {
            using (var context = NewContext())
            {
                for (var day = 1; day <= 4; day++)
                    context.CatalogSyncRuns.Add(new CatalogSyncRun
                    {
                        StartedUTC = new DateTime(2026, 10, day, 4, 0, 0, DateTimeKind.Utc),
                        Status = CatalogSyncRunStatus.Succeeded,
                        TCGPlayerStatus = CatalogSourceStatus.Succeeded,
                        YamlYugiStatus = day == 1 ? CatalogSourceStatus.Succeeded : CatalogSourceStatus.Failed,
                        YGOProDeckStatus = CatalogSourceStatus.Succeeded
                    });
                await context.SaveChangesAsync();
            }

            var history = await Service().GetSyncHistoryAsync(1, 25);

            Assert.AreEqual(4, history.Runs.TotalCount);
            var streak = history.FailureStreaks.Single();
            Assert.AreEqual(CatalogSource.YamlYugi, streak.Source);
            Assert.AreEqual(3, streak.RunCount);
        }

        [TestInitialize]
        public void Initialize()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            _cardDataRepositoryMock = new Mock<ICardDataRepository>();
            _cardDataRepositoryMock.Setup(r => r.AllRarityNames).Returns([]);

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
        public async Task MergePrintingAsync_DifferentCards_Fails()
        {
            var otherCardPrintingID = await AddCardWithPrintingAsync("Synthetic Artmage", 74733322, "DUAD-EN054");

            var result = await Service().MergePrintingAsync(_printingID, otherCardPrintingID);

            Assert.IsFalse(result.Succeeded);
        }

        [TestMethod]
        public async Task MergePrintingAsync_SurvivorHasWishlistAndCheckout_FoldsRows()
        {
            var intoID = await AddPrintingAsync("Ultra Pharaoh’s Rare");
            using (var context = NewContext())
            {
                context.PreferredVersions.AddRange(
                    new PreferredVersion { CatalogPrintingID = _printingID, DesiredQuantity = 3 },
                    new PreferredVersion { CatalogPrintingID = intoID, DesiredQuantity = 1 });
                context.CheckedOutCards.AddRange(
                    new CheckedOutCard { CatalogPrintingID = _printingID, Quantity = 1 },
                    new CheckedOutCard { CatalogPrintingID = intoID, Quantity = 2 });
                await context.SaveChangesAsync();
            }

            var result = await Service().MergePrintingAsync(_printingID, intoID);

            Assert.IsTrue(result.Succeeded);
            using var verify = NewContext();
            Assert.AreEqual(3, (await verify.PreferredVersions.SingleAsync()).DesiredQuantity);
            Assert.AreEqual(3, (await verify.CheckedOutCards.SingleAsync()).Quantity);
        }

        [TestMethod]
        public async Task MergePrintingAsync_SurvivorHasNoRows_MovesRowsAndLinksAndRetiresMergedPrinting()
        {
            var intoID = await AddPrintingAsync("Ultra Pharaoh’s Rare");
            using (var context = NewContext())
            {
                context.CollectionEntries.Add(new CollectionEntry
                {
                    CatalogPrintingID = _printingID, Quantity = 1, Status = CollectionStatus.Owned
                });
                context.CatalogPrintingSources.Add(new CatalogPrintingSource { CatalogPrintingID = _printingID, ExternalKey = "695684", Source = CatalogSource.TCGPlayer });
                await context.SaveChangesAsync();
            }

            var result = await Service().MergePrintingAsync(_printingID, intoID);

            Assert.IsTrue(result.Succeeded);
            using var verify = NewContext();
            var entry = await verify.CollectionEntries.SingleAsync();
            Assert.AreEqual(intoID, entry.CatalogPrintingID);
            Assert.AreEqual(intoID, (await verify.CatalogPrintingSources.SingleAsync()).CatalogPrintingID);
            Assert.AreEqual(CatalogStatus.Retired, (await verify.CatalogPrintings.SingleAsync(p => p.ID == _printingID)).Status);
        }

        [TestMethod]
        [DataRow("48739627", DisplayName = "Passcode")]
        [DataRow("blgg", DisplayName = "Set code prefix")]
        [DataRow("theo", DisplayName = "Part of the name")]
        public async Task SearchCardsAsync_MatchingQuery_FindsCard(string query)
        {
            await AddCardWithPrintingAsync("Synthetic Artmage", 74733322, "DUAD-EN054");

            var results = await Service().SearchCardsAsync(new CatalogCardSearchCriteria { Query = query });

            Assert.AreEqual("Synthetic Theory", results.Items.Single().Name);
        }

        [TestMethod]
        public async Task SetPrintingStatusAsync_RetireWhileInUse_FailsAndLeavesItActive()
        {
            using (var context = NewContext())
            {
                context.CollectionEntries.Add(new CollectionEntry
                { CatalogPrintingID = _printingID, Quantity = 1, Status = CollectionStatus.Owned
                });
                await context.SaveChangesAsync();
            }

            var result = await Service().SetPrintingStatusAsync(_printingID, CatalogStatus.Retired);

            Assert.IsFalse(result.Succeeded);
            using var verify = NewContext();
            Assert.AreEqual(CatalogStatus.Active, (await verify.CatalogPrintings.SingleAsync()).Status);
        }

        [TestMethod]
        public async Task SetPrintingStatusAsync_RetireUnused_RetiresIt()
        {
            var result = await Service().SetPrintingStatusAsync(_printingID, CatalogStatus.Retired);

            Assert.IsTrue(result.Succeeded);
            using var verify = NewContext();
            Assert.AreEqual(CatalogStatus.Retired, (await verify.CatalogPrintings.SingleAsync()).Status);
        }

        [TestMethod]
        public async Task UnlinkSourceAsync_ManualSource_Fails()
        {
            var sourceID = await AddSourceAsync(CatalogSource.Manual, "card:1|BLGG-EN127|Ultra Rare|");

            var result = await Service().UnlinkSourceAsync(sourceID);

            Assert.IsFalse(result.Succeeded);
        }

        [TestMethod]
        public async Task UnlinkSourceAsync_ProductLinkedFromInbox_RemovesLinkAndReopensItem()
        {
            var sourceID = await AddSourceAsync(CatalogSource.TCGPlayer, "695684");
            using (var context = NewContext())
            {
                context.CatalogReviewItems.Add(new CatalogReviewItem
                {
                    Kind = CatalogReviewItemKind.UnmappedTCGProduct,
                    PayloadHash = "hash",
                    Resolution = "Linked to BLGG-EN127 Ultra Rare",
                    ResolvedUTC = CatalogTestData.EarlierUTC,
                    Status = CatalogReviewItemStatus.Accepted,
                    SubjectKey = "tcg:695684",
                    Summary = "Synthetic summary"
                });
                await context.SaveChangesAsync();
            }

            var result = await Service().UnlinkSourceAsync(sourceID);

            Assert.IsTrue(result.Succeeded);
            using var verify = NewContext();
            Assert.AreEqual(0, await verify.CatalogPrintingSources.CountAsync());
            var item = await verify.CatalogReviewItems.SingleAsync();
            Assert.AreEqual(CatalogReviewItemStatus.Open, item.Status);
            Assert.IsNull(item.ResolvedUTC);
        }

        [TestMethod]
        public async Task UnlockFieldAsync_LockedField_UnlocksIt()
        {
            using (var context = NewContext())
            {
                (await context.CatalogCards.SingleAsync()).LockedFields = "Name,Passcode";
                await context.SaveChangesAsync();
            }

            var result = await Service().UnlockFieldAsync(_cardID, "passcode");

            Assert.IsTrue(result.Succeeded);
            using var verify = NewContext();
            Assert.AreEqual("Name", (await verify.CatalogCards.SingleAsync()).LockedFields);
        }

        [TestMethod]
        public async Task UnlockFieldAsync_UnknownField_Fails()
        {
            var result = await Service().UnlockFieldAsync(_cardID, "Rarity");

            Assert.IsFalse(result.Succeeded);
        }

        [TestMethod]
        public async Task UpdateCardAsync_ChangedFields_SavesAndLocksOnlyThose()
        {
            var input = new CatalogCardEditInput { ATK = 2500, Name = "Synthetic Theory", Passcode = 74733322 };

            var result = await Service().UpdateCardAsync(_cardID, input);

            Assert.IsTrue(result.Succeeded);
            using var verify = NewContext();
            var card = await verify.CatalogCards.SingleAsync();
            Assert.AreEqual(74733322, card.Passcode);
            Assert.AreEqual(2500, card.ATK);
            Assert.AreEqual("ATK,Passcode", card.LockedFields);
            _cardDataRepositoryMock.Verify(r => r.QueueReload(), Times.Once);
        }

        [TestMethod]
        public async Task UpdateCardAsync_PasscodeBelongsToAnotherCard_FailsWithoutSaving()
        {
            await AddCardWithPrintingAsync("Synthetic Artmage", 74733322, "DUAD-EN054");

            var result = await Service().UpdateCardAsync(_cardID, new CatalogCardEditInput { Name = "Synthetic Theory", Passcode = 74733322 });

            Assert.IsFalse(result.Succeeded);
            using var verify = NewContext();
            Assert.AreEqual(48739627, (await verify.CatalogCards.SingleAsync(c => c.ID == _cardID)).Passcode);
        }

        private async Task<int> AddCardWithPrintingAsync(string name, int passcode, string setCode)
        {
            using var context = NewContext();
            var card = CatalogTestData.Card(0, name, passcode: passcode);
            var printing = CatalogTestData.Printing(card, 0, setCode, "Ultra Rare");
            printing.CatalogCardID = 0;
            context.CatalogCards.Add(card);
            await context.SaveChangesAsync();
            return printing.ID;
        }

        private async Task<int> AddPrintingAsync(string rarityName)
        {
            using var context = NewContext();
            var set = await context.CatalogSets.SingleAsync();
            var printing = new CatalogPrinting
            {
                CatalogCardID = _cardID, CatalogSetID = set.ID, RarityName = rarityName, SetCode = "BLGG-EN127", Status = CatalogStatus.Active
            };
            context.CatalogPrintings.Add(printing);
            await context.SaveChangesAsync();
            return printing.ID;
        }

        private async Task<int> AddSourceAsync(CatalogSource source, string externalKey)
        {
            using var context = NewContext();
            var link = new CatalogPrintingSource { CatalogPrintingID = _printingID, ExternalKey = externalKey, Source = source };
            context.CatalogPrintingSources.Add(link);
            await context.SaveChangesAsync();
            return link.ID;
        }

        private AppDBContext NewContext() =>
            new(new DbContextOptionsBuilder<AppDBContext>().UseSqlite(_connection).Options);

        private CatalogAdminService Service()
        {
            var context = NewContext();
            return new CatalogAdminService(
                new CatalogAdminRepository(context),
                _cardDataRepositoryMock.Object,
                NullLogger<CatalogAdminService>.Instance,
                Options.Create(new CatalogSyncSettings()),
                new UnitOfWork(context),
                new FakeTimeProvider(new DateTimeOffset(2026, 10, 13, 4, 0, 0, TimeSpan.Zero)));
        }
    }
}
