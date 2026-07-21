using CardCollector.Data.Models;
using CardCollector.Repository;
using CardCollector.Tests.TestHelpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CardCollector.Tests.Repository
{
    [TestClass]
    public sealed class PendingOrderRepositoryTests
    {
        [TestMethod]
        public async Task AddRangeAsync_ThenGetAllAsync_OrdersByDateCreatedDescending()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new PendingOrderRepository(context);
            await repository.AddRangeAsync(
            [
                new PendingOrderLine { CardID = 1, ImageID = 10, SetCode = "OLD-EN001", DateCreated = new DateTime(2020, 1, 1) },
                new PendingOrderLine { CardID = 2, ImageID = 20, SetCode = "NEW-EN001", DateCreated = new DateTime(2026, 1, 1) }
            ]);

            var result = await repository.GetAllAsync();

            Assert.AreEqual("NEW-EN001", result[0].SetCode);
            Assert.AreEqual("OLD-EN001", result[1].SetCode);
        }

        [TestMethod]
        public async Task DeleteRangeAsync_EmptyIDs_DoesNothing()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new PendingOrderRepository(context);
            await repository.AddRangeAsync([new PendingOrderLine { CardID = 1, ImageID = 10, SetCode = "LOB-EN001" }]);

            await repository.DeleteRangeAsync([]);

            Assert.AreEqual(1, (await repository.GetAllAsync()).Count);
        }

        [TestMethod]
        public async Task DeleteRangeAsync_RemovesOnlyMatchingLines()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new PendingOrderRepository(context);
            var line1 = new PendingOrderLine { CardID = 1, ImageID = 10, SetCode = "LOB-EN001" };
            var line2 = new PendingOrderLine { CardID = 2, ImageID = 20, SetCode = "LOB-EN002" };
            await repository.AddRangeAsync([line1, line2]);

            await repository.DeleteRangeAsync([line1.ID]);

            var remaining = await repository.GetAllAsync();
            Assert.AreEqual(1, remaining.Count);
            Assert.AreEqual("LOB-EN002", remaining[0].SetCode);
        }
        [TestMethod]
        public async Task GetByIDsAsync_EmptyIDs_ReturnsEmpty()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new PendingOrderRepository(context);

            var result = await repository.GetByIDsAsync([]);

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public async Task GetStagedQuantitiesAsync_GroupsByImageSetAndRarityNullBecomesEmptyString()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new PendingOrderRepository(context);
            await repository.AddRangeAsync(
            [
                new PendingOrderLine { CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = null, Quantity = 2 },
                new PendingOrderLine { CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = null, Quantity = 1 }
            ]);

            var result = await repository.GetStagedQuantitiesAsync();

            Assert.AreEqual(3, result[(10, "LOB-EN001", "")]);
        }

        [TestMethod]
        public async Task GetSummaryAsync_SumsPriceTimesQuantityTreatingNullPriceAsZero()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new PendingOrderRepository(context);
            await repository.AddRangeAsync(
            [
                new PendingOrderLine { CardID = 1, ImageID = 10, SetCode = "LOB-EN001", Quantity = 2, PurchasePrice = 5m },
                new PendingOrderLine { CardID = 2, ImageID = 20, SetCode = "LOB-EN002", Quantity = 1, PurchasePrice = null }
            ]);

            var (count, total) = await repository.GetSummaryAsync();

            Assert.AreEqual(2, count);
            Assert.AreEqual(10m, total);
        }

        [TestMethod]
        public async Task UpdateQuantityAsync_ExistingLine_UpdatesQuantity()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new PendingOrderRepository(context);
            var line = new PendingOrderLine { CardID = 1, ImageID = 10, SetCode = "LOB-EN001", Quantity = 1 };
            await repository.AddRangeAsync([line]);

            var result = await repository.UpdateQuantityAsync(line.ID, 3);

            Assert.IsTrue(result);
            var lines = await repository.GetAllAsync();
            Assert.AreEqual(3, lines[0].Quantity);
        }

        [TestMethod]
        public async Task UpdateQuantityAsync_NoSuchLine_ReturnsFalse()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new PendingOrderRepository(context);

            var result = await repository.UpdateQuantityAsync(999, 2);

            Assert.IsFalse(result);
        }
    }
}
