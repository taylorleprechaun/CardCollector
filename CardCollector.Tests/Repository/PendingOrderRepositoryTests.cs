using CardCollector.Data.Models;
using CardCollector.Repository;
using CardCollector.Tests.TestHelpers;

namespace CardCollector.Tests.Repository
{
    [TestClass]
    public sealed class PendingOrderRepositoryTests
    {
        [TestMethod]
        public async Task AddRangeAsync_ThenGetAllAsync_OrdersByDateCreatedDescending()
        {
            using var context = InMemoryDbContextFactory.Create();
            CatalogTestData.SeedPrintings(context, (101, 1), (201, 2));
            var repository = new PendingOrderRepository(context);
            await repository.AddRangeAsync(
            [
                new PendingOrderLine { CatalogPrintingID = 101, DateCreated = new DateTime(2020, 1, 1) },
                new PendingOrderLine { CatalogPrintingID = 201, DateCreated = new DateTime(2026, 1, 1) }
            ]);

            var result = await repository.GetAllAsync();

            Assert.AreEqual(201, result[0].CatalogPrintingID);
            Assert.AreEqual(101, result[1].CatalogPrintingID);
        }

        [TestMethod]
        public async Task GetStagedQuantitiesAsync_SumsStagedQuantityPerPrinting()
        {
            using var context = InMemoryDbContextFactory.Create();
            CatalogTestData.SeedPrintings(context, (101, 1), (102, 1));
            var repository = new PendingOrderRepository(context);
            await repository.AddRangeAsync(
            [
                new PendingOrderLine { CatalogPrintingID = 101, Quantity = 2 },
                new PendingOrderLine { CatalogPrintingID = 101, Quantity = 1 },
                new PendingOrderLine { CatalogPrintingID = 102, Quantity = 1 }
            ]);

            var result = await repository.GetStagedQuantitiesAsync();

            Assert.AreEqual(3, result[101]);
            Assert.AreEqual(1, result[102]);
        }

        [TestMethod]
        public async Task DeleteAsync_ExistingLine_RemovesItAndReturnsTrue()
        {
            using var context = InMemoryDbContextFactory.Create();
            CatalogTestData.SeedPrintings(context, (101, 1));
            var repository = new PendingOrderRepository(context);
            var line = new PendingOrderLine { CatalogPrintingID = 101 };
            await repository.AddRangeAsync([line]);

            var result = await repository.DeleteAsync(line.ID);

            Assert.IsTrue(result);
            Assert.AreEqual(0, (await repository.GetAllAsync()).Count);
        }

        [TestMethod]
        public async Task DeleteAsync_NoSuchLine_ReturnsFalse()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new PendingOrderRepository(context);

            var result = await repository.DeleteAsync(999);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task DeleteRangeAsync_EmptyIDs_DoesNothing()
        {
            using var context = InMemoryDbContextFactory.Create();
            CatalogTestData.SeedPrintings(context, (101, 1));
            var repository = new PendingOrderRepository(context);
            await repository.AddRangeAsync([new PendingOrderLine { CatalogPrintingID = 101 }]);

            await repository.DeleteRangeAsync([]);

            Assert.AreEqual(1, (await repository.GetAllAsync()).Count);
        }

        [TestMethod]
        public async Task DeleteRangeAsync_RemovesOnlyMatchingLines()
        {
            using var context = InMemoryDbContextFactory.Create();
            CatalogTestData.SeedPrintings(context, (101, 1), (201, 2));
            var repository = new PendingOrderRepository(context);
            var line1 = new PendingOrderLine { CatalogPrintingID = 101 };
            var line2 = new PendingOrderLine { CatalogPrintingID = 201 };
            await repository.AddRangeAsync([line1, line2]);

            await repository.DeleteRangeAsync([line1.ID]);

            var remaining = await repository.GetAllAsync();
            Assert.AreEqual(1, remaining.Count);
            Assert.AreEqual(201, remaining[0].CatalogPrintingID);
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
        public async Task GetByIDsAsync_MatchingIDs_ReturnsOnlyMatchesOrderedByDateCreatedDescending()
        {
            using var context = InMemoryDbContextFactory.Create();
            CatalogTestData.SeedPrintings(context, (101, 1), (201, 2), (301, 3));
            var repository = new PendingOrderRepository(context);
            await repository.AddRangeAsync(
            [
                new PendingOrderLine { CatalogPrintingID = 101, Quantity = 1, DateCreated = new DateTime(2026, 1, 1) },
                new PendingOrderLine { CatalogPrintingID = 201, Quantity = 1, DateCreated = new DateTime(2026, 2, 1) },
                new PendingOrderLine { CatalogPrintingID = 301, Quantity = 1, DateCreated = new DateTime(2026, 3, 1) }
            ]);
            var allLines = await repository.GetAllAsync();
            var matchingIDs = allLines.Where(l => l.CatalogPrintingID != 301).Select(l => l.ID);

            var result = await repository.GetByIDsAsync(matchingIDs);

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(201, result[0].CatalogPrintingID);
            Assert.AreEqual(101, result[1].CatalogPrintingID);
        }

        [TestMethod]
        public async Task GetSummaryAsync_SumsPriceTimesQuantityTreatingNullPriceAsZero()
        {
            using var context = InMemoryDbContextFactory.Create();
            CatalogTestData.SeedPrintings(context, (101, 1), (201, 2));
            var repository = new PendingOrderRepository(context);
            await repository.AddRangeAsync(
            [
                new PendingOrderLine { CatalogPrintingID = 101, Quantity = 2, PurchasePrice = 5m },
                new PendingOrderLine { CatalogPrintingID = 201, Quantity = 1, PurchasePrice = null }
            ]);

            var (count, total) = await repository.GetSummaryAsync();

            Assert.AreEqual(2, count);
            Assert.AreEqual(10m, total);
        }

        [TestMethod]
        public async Task UpdateAsync_ExistingLine_SavesEditableFieldsAndKeepsThePrinting()
        {
            using var context = InMemoryDbContextFactory.Create();
            CatalogTestData.SeedPrintings(context, (101, 1));
            var repository = new PendingOrderRepository(context);
            var line = new PendingOrderLine { CatalogPrintingID = 101, Quantity = 1 };
            await repository.AddRangeAsync([line]);

            var result = await repository.UpdateAsync(new PendingOrderLine
            {
                Condition = CardCondition.LightlyPlayed,
                Edition = CardEdition.Unlimited,
                ID = line.ID,
                MarketPriceAtEntry = 4.25m,
                PurchaseDate = new DateTime(2026, 10, 20),
                PurchasePrice = 3.50m,
                Quantity = 3
            });

            Assert.IsTrue(result);
            var saved = (await repository.GetAllAsync()).Single();
            Assert.AreEqual(101, saved.CatalogPrintingID);
            Assert.AreEqual(CardCondition.LightlyPlayed, saved.Condition);
            Assert.AreEqual(CardEdition.Unlimited, saved.Edition);
            Assert.AreEqual(4.25m, saved.MarketPriceAtEntry);
            Assert.AreEqual(new DateTime(2026, 10, 20), saved.PurchaseDate);
            Assert.AreEqual(3.50m, saved.PurchasePrice);
            Assert.AreEqual(3, saved.Quantity);
        }

        [TestMethod]
        public async Task UpdateAsync_NoSuchLine_ReturnsFalse()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new PendingOrderRepository(context);

            var result = await repository.UpdateAsync(new PendingOrderLine { ID = 999, Quantity = 2 });

            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task UpdateAsync_NullLine_ThrowsArgumentNullException()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new PendingOrderRepository(context);

            await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => repository.UpdateAsync(null!));
        }
    }
}
