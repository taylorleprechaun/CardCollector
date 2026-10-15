using CardCollector.Data.Models;
using CardCollector.Repository;
using CardCollector.Tests.TestHelpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CardCollector.Tests.Repository
{
    [TestClass]
    public sealed class CollectionEntryValueRepositoryTests
    {
        [TestMethod]
        public async Task GetCatalogCardIDsAsync_ReturnsDistinctCardsAndSkipsUnlinkedSnapshots()
        {
            using var context = InMemoryDbContextFactory.Create();
            context.CollectionEntryValueSnapshots.AddRange(
                new CollectionEntryValueSnapshot { CatalogCardID = 2, SnapshotDate = "2026-01-01" },
                new CollectionEntryValueSnapshot { CatalogCardID = 1, SnapshotDate = "2026-01-01" },
                new CollectionEntryValueSnapshot { CatalogCardID = 1, SnapshotDate = "2026-01-02" },
                new CollectionEntryValueSnapshot { CatalogCardID = null, SnapshotDate = "2026-01-02" });
            await context.SaveChangesAsync();
            var repository = new CollectionEntryValueRepository(context);

            var result = await repository.GetCatalogCardIDsAsync();

            CollectionAssert.AreEquivalent(new[] { 1, 2 }, result.ToArray());
        }

        [TestMethod]
        public async Task GetHistoryByCardIDAsync_ReturnsThatCardsSnapshotsBySnapshotDateAscending()
        {
            using var context = InMemoryDbContextFactory.Create();
            context.CollectionEntryValueSnapshots.AddRange(
                new CollectionEntryValueSnapshot { CardName = "Old Name", CatalogCardID = 1, SnapshotDate = "2026-02-01" },
                new CollectionEntryValueSnapshot { CardName = "New Name", CatalogCardID = 1, SnapshotDate = "2026-01-01" },
                new CollectionEntryValueSnapshot { CardName = "Other Card", CatalogCardID = 2, SnapshotDate = "2026-01-01" });
            await context.SaveChangesAsync();
            var repository = new CollectionEntryValueRepository(context);

            var result = (await repository.GetHistoryByCardIDAsync(1)).ToList();

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("2026-01-01", result[0].SnapshotDate);
            Assert.AreEqual("2026-02-01", result[1].SnapshotDate);
        }

        [TestMethod]
        public async Task GetLatestSnapshotsAsync_ReturnsOnlyMostRecentPerEntry()
        {
            using var context = InMemoryDbContextFactory.Create();
            context.CollectionEntryValueSnapshots.AddRange(
                new CollectionEntryValueSnapshot { CollectionEntryID = 1, SnapshotDate = "2026-01-01", MarketValue = 10m },
                new CollectionEntryValueSnapshot { CollectionEntryID = 1, SnapshotDate = "2026-02-01", MarketValue = 20m },
                new CollectionEntryValueSnapshot { CollectionEntryID = 2, SnapshotDate = "2026-01-15", MarketValue = 5m });
            await context.SaveChangesAsync();
            var repository = new CollectionEntryValueRepository(context);

            var result = (await repository.GetLatestSnapshotsAsync()).ToList();

            Assert.AreEqual(2, result.Count);
            Assert.IsTrue(result.Any(s => s.CollectionEntryID == 1 && s.MarketValue == 20m));
            Assert.IsTrue(result.Any(s => s.CollectionEntryID == 2 && s.MarketValue == 5m));
        }

        [TestMethod]
        public async Task UpsertSnapshotsAsync_ReplacesExistingRowsForSameDate()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new CollectionEntryValueRepository(context);
            await repository.UpsertSnapshotsAsync([new CollectionEntryValueSnapshot { CollectionEntryID = 1, SnapshotDate = "2026-01-01", MarketValue = 10m }], "2026-01-01");

            await repository.UpsertSnapshotsAsync([new CollectionEntryValueSnapshot { CollectionEntryID = 1, SnapshotDate = "2026-01-01", MarketValue = 99m }], "2026-01-01");

            var snapshotsForDate = context.CollectionEntryValueSnapshots.Where(s => s.SnapshotDate == "2026-01-01").ToList();
            Assert.AreEqual(1, snapshotsForDate.Count);
            Assert.AreEqual(99m, snapshotsForDate[0].MarketValue);
        }
    }
}
