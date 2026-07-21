using CardCollector.Data.Models;
using CardCollector.Repository;
using CardCollector.Tests.TestHelpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CardCollector.Tests.Repository
{
    [TestClass]
    public sealed class CollectionValueRepositoryTests
    {
        [TestMethod]
        public async Task GetAllSnapshotsAsync_OrdersAscendingByDate()
        {
            using var context = InMemoryDbContextFactory.Create();
            context.CollectionValueSnapshots.AddRange(
                new CollectionValueSnapshot { SnapshotDate = "2026-02-01", TotalValue = 20m },
                new CollectionValueSnapshot { SnapshotDate = "2026-01-01", TotalValue = 10m });
            await context.SaveChangesAsync();
            var repository = new CollectionValueRepository(context);

            var result = (await repository.GetAllSnapshotsAsync()).ToList();

            Assert.AreEqual("2026-01-01", result[0].SnapshotDate);
        }

        [TestMethod]
        public async Task GetLatestSnapshotAsync_NoSnapshots_ReturnsNull()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new CollectionValueRepository(context);

            var result = await repository.GetLatestSnapshotAsync();

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetLatestSnapshotAsync_ReturnsMostRecentByDate()
        {
            using var context = InMemoryDbContextFactory.Create();
            context.CollectionValueSnapshots.AddRange(
                new CollectionValueSnapshot { SnapshotDate = "2026-01-01", TotalValue = 10m },
                new CollectionValueSnapshot { SnapshotDate = "2026-02-01", TotalValue = 20m });
            await context.SaveChangesAsync();
            var repository = new CollectionValueRepository(context);

            var result = await repository.GetLatestSnapshotAsync();

            Assert.AreEqual(20m, result!.TotalValue);
        }
        [TestMethod]
        public async Task UpsertSnapshotAsync_ExistingSnapshotForDate_UpdatesInPlace()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new CollectionValueRepository(context);
            await repository.UpsertSnapshotAsync(new CollectionValueSnapshot { SnapshotDate = "2026-01-01", TotalValue = 10m, CardCount = 5 });

            await repository.UpsertSnapshotAsync(new CollectionValueSnapshot { SnapshotDate = "2026-01-01", TotalValue = 99m, CardCount = 7 });

            var result = (await repository.GetAllSnapshotsAsync()).ToList();
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(99m, result[0].TotalValue);
            Assert.AreEqual(7, result[0].CardCount);
        }

        [TestMethod]
        public async Task UpsertSnapshotAsync_NoExistingSnapshotForDate_Inserts()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new CollectionValueRepository(context);

            await repository.UpsertSnapshotAsync(new CollectionValueSnapshot { SnapshotDate = "2026-01-01", TotalValue = 10m, CardCount = 5 });

            var result = await repository.GetLatestSnapshotAsync();
            Assert.AreEqual(10m, result!.TotalValue);
        }
    }
}
