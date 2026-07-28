using CardCollector.Data.Models;
using CardCollector.Repository;
using CardCollector.Tests.TestHelpers;

namespace CardCollector.Tests.Repository
{
    [TestClass]
    public sealed class WishlistValueRepositoryTests
    {
        [TestMethod]
        public async Task GetAllSnapshotsAsync_OrdersAscendingByDate()
        {
            using var context = InMemoryDbContextFactory.Create();
            context.WishlistValueSnapshots.AddRange(
                new WishlistValueSnapshot { SnapshotDate = "2026-02-01", TotalValue = 20m },
                new WishlistValueSnapshot { SnapshotDate = "2026-01-01", TotalValue = 10m });
            await context.SaveChangesAsync();
            var repository = new WishlistValueRepository(context);

            var result = (await repository.GetAllSnapshotsAsync()).ToList();

            Assert.AreEqual("2026-01-01", result[0].SnapshotDate);
        }

        [TestMethod]
        public async Task GetLatestSnapshotAsync_NoSnapshots_ReturnsNull()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new WishlistValueRepository(context);

            var result = await repository.GetLatestSnapshotAsync();

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetLatestSnapshotAsync_ReturnsMostRecentByDate()
        {
            using var context = InMemoryDbContextFactory.Create();
            context.WishlistValueSnapshots.AddRange(
                new WishlistValueSnapshot { SnapshotDate = "2026-01-01", TotalValue = 10m },
                new WishlistValueSnapshot { SnapshotDate = "2026-02-01", TotalValue = 20m });
            await context.SaveChangesAsync();
            var repository = new WishlistValueRepository(context);

            var result = await repository.GetLatestSnapshotAsync();

            Assert.AreEqual(20m, result!.TotalValue);
        }

        [TestMethod]
        public async Task PruneSnapshotsAsync_KeepsRecentAndOnePerMonth_DeletesRest()
        {
            var (context, connection) = InMemoryDbContextFactory.CreateSqlite();
            using (connection)
            using (context)
            {
                var repository = new WishlistValueRepository(context);
                var now = DateTime.UtcNow;

                // Well within the 30-day cutoff — always kept regardless of month grouping.
                var recentDate = now.AddDays(-5).ToString("yyyy-MM-dd");

                // Two snapshots in the same calendar month, both beyond the cutoff — only the later one
                // (the month's max) should survive pruning. Anchored to day-of-month 1 so adding a few
                // days never rolls into a different month.
                var oldMonth = new DateTime(now.AddMonths(-2).Year, now.AddMonths(-2).Month, 1, 0, 0, 0, DateTimeKind.Utc);
                var oldMonthLaterDate = oldMonth.AddDays(19).ToString("yyyy-MM-dd");
                var oldMonthEarlierDate = oldMonth.AddDays(9).ToString("yyyy-MM-dd");

                // A different calendar month, further back — its single snapshot is that month's max, so it survives.
                var differentOldMonthDate = oldMonth.AddMonths(-2).ToString("yyyy-MM-dd");

                context.WishlistValueSnapshots.AddRange(
                    new WishlistValueSnapshot { SnapshotDate = recentDate, TotalValue = 1m, RemainingCount = 1 },
                    new WishlistValueSnapshot { SnapshotDate = oldMonthLaterDate, TotalValue = 2m, RemainingCount = 2 },
                    new WishlistValueSnapshot { SnapshotDate = oldMonthEarlierDate, TotalValue = 3m, RemainingCount = 3 },
                    new WishlistValueSnapshot { SnapshotDate = differentOldMonthDate, TotalValue = 4m, RemainingCount = 4 });
                await context.SaveChangesAsync();

                await repository.PruneSnapshotsAsync();

                var remainingDates = (await repository.GetAllSnapshotsAsync()).Select(s => s.SnapshotDate).ToList();
                CollectionAssert.AreEquivalent(
                    new[] { recentDate, oldMonthLaterDate, differentOldMonthDate },
                    remainingDates);
            }
        }

        [TestMethod]
        public async Task UpsertSnapshotAsync_ExistingSnapshotForDate_UpdatesInPlace()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new WishlistValueRepository(context);
            await repository.UpsertSnapshotAsync(new WishlistValueSnapshot { SnapshotDate = "2026-01-01", TotalValue = 10m, RemainingCount = 5 });

            await repository.UpsertSnapshotAsync(new WishlistValueSnapshot { SnapshotDate = "2026-01-01", TotalValue = 99m, RemainingCount = 7 });

            var result = (await repository.GetAllSnapshotsAsync()).ToList();
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(99m, result[0].TotalValue);
            Assert.AreEqual(7, result[0].RemainingCount);
        }

        [TestMethod]
        public async Task UpsertSnapshotAsync_NoExistingSnapshotForDate_Inserts()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new WishlistValueRepository(context);

            await repository.UpsertSnapshotAsync(new WishlistValueSnapshot { SnapshotDate = "2026-01-01", TotalValue = 10m, RemainingCount = 5 });

            var result = await repository.GetLatestSnapshotAsync();
            Assert.AreEqual(10m, result!.TotalValue);
        }
    }
}
