using CardCollector.Data.Models;
using CardCollector.Repository;
using CardCollector.Tests.TestHelpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CardCollector.Tests.Repository
{
    [TestClass]
    public sealed class PreferredVersionRepositoryTests
    {
        [TestMethod]
        public async Task AddOrUpdateAsync_DifferentPrintingSameCard_CreatesSecondRecord()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new PreferredVersionRepository(context);
            await repository.AddOrUpdateAsync(101, 1);

            await repository.AddOrUpdateAsync(102, 1);

            var result = await repository.GetByCardIDAsync(1);
            CollectionAssert.AreEquivalent(new int?[] { 101, 102 }, result.Select(r => r.CatalogPrintingID).ToArray());
        }

        [TestMethod]
        public async Task AddOrUpdateAsync_NoExistingRecord_CreatesNewForThePrinting()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new PreferredVersionRepository(context);

            await repository.AddOrUpdateAsync(101, 1);

            var result = (await repository.GetByCardIDAsync(1)).Single();
            Assert.AreEqual(101, result.CatalogPrintingID);
        }

        [TestMethod]
        public async Task AddOrUpdateAsync_NoExistingRecordDesiredQuantityGiven_UsesGivenValue()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new PreferredVersionRepository(context);

            await repository.AddOrUpdateAsync(101, 1, desiredQuantity: 1);

            var result = (await repository.GetByCardIDAsync(1)).Single();
            Assert.AreEqual(1, result.DesiredQuantity);
        }

        [TestMethod]
        public async Task AddOrUpdateAsync_NoExistingRecordNoDesiredQuantityGiven_DefaultsToThree()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new PreferredVersionRepository(context);

            await repository.AddOrUpdateAsync(101, 1);

            var result = (await repository.GetByCardIDAsync(1)).Single();
            Assert.AreEqual(3, result.DesiredQuantity);
        }

        [TestMethod]
        public async Task AddOrUpdateAsync_SamePrintingDesiredQuantityGiven_OverwritesValue()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new PreferredVersionRepository(context);
            await repository.AddOrUpdateAsync(101, 1, desiredQuantity: 1);

            await repository.AddOrUpdateAsync(101, 1, desiredQuantity: 2);

            var result = (await repository.GetByCardIDAsync(1)).Single();
            Assert.AreEqual(2, result.DesiredQuantity);
        }

        [TestMethod]
        public async Task AddOrUpdateAsync_SamePrintingNoDesiredQuantityGiven_PreservesExistingValue()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new PreferredVersionRepository(context);
            await repository.AddOrUpdateAsync(101, 1, desiredQuantity: 1);

            await repository.AddOrUpdateAsync(101, 1);

            var result = (await repository.GetByCardIDAsync(1)).Single();
            Assert.AreEqual(1, result.DesiredQuantity);
            Assert.AreEqual(1, (await repository.GetAllAsync()).Count());
        }

        [TestMethod]
        public async Task DeleteAsync_ExistingID_RemovesRecord()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new PreferredVersionRepository(context);
            await repository.AddOrUpdateAsync(101, 1);
            var created = (await repository.GetByCardIDAsync(1)).Single();

            await repository.DeleteAsync(created.ID);

            Assert.AreEqual(0, (await repository.GetByCardIDAsync(1)).Count);
        }

        [TestMethod]
        public async Task DeleteAsync_NoSuchID_DoesNotThrow()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new PreferredVersionRepository(context);

            await repository.DeleteAsync(999);
        }

        [TestMethod]
        public async Task GetByCardIDAsync_NoRecords_ReturnsEmpty()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new PreferredVersionRepository(context);

            var result = await repository.GetByCardIDAsync(999);

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public async Task GetByCardIDsAsync_EmptyInput_ReturnsEmpty()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new PreferredVersionRepository(context);

            var result = await repository.GetByCardIDsAsync([]);

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public async Task GetByCardIDsAsync_MatchingIDs_ReturnsKeyedByCardID()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new PreferredVersionRepository(context);
            await repository.AddOrUpdateAsync(101, 1);

            var result = await repository.GetByCardIDsAsync([1, 999]);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(101, result[1].Single().CatalogPrintingID);
        }

        [TestMethod]
        public async Task GetByCardIDsAsync_TwoTrackedPrintingsForSameCard_BothReturnedForThatKey()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new PreferredVersionRepository(context);
            await repository.AddOrUpdateAsync(101, 1);
            await repository.AddOrUpdateAsync(102, 1);

            var result = await repository.GetByCardIDsAsync([1]);

            Assert.AreEqual(2, result[1].Count);
        }

        [TestMethod]
        public async Task GetPreferredCardIDsAsync_ReturnsDistinctCardIDs()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new PreferredVersionRepository(context);
            await repository.AddOrUpdateAsync(101, 1);
            await repository.AddOrUpdateAsync(102, 1);
            await repository.AddOrUpdateAsync(201, 2);

            var result = await repository.GetPreferredCardIDsAsync();

            CollectionAssert.AreEquivalent(new[] { 1, 2 }, result.ToArray());
        }

        [TestMethod]
        public async Task UpdateDesiredQuantityAsync_ExistingID_UpdatesAndReturnsTrue()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new PreferredVersionRepository(context);
            await repository.AddOrUpdateAsync(101, 1);
            var created = (await repository.GetByCardIDAsync(1)).Single();

            var result = await repository.UpdateDesiredQuantityAsync(created.ID, 1);

            Assert.IsTrue(result);
            var updated = (await repository.GetByCardIDAsync(1)).Single();
            Assert.AreEqual(1, updated.DesiredQuantity);
        }

        [TestMethod]
        public async Task UpdateDesiredQuantityAsync_NoSuchID_ReturnsFalse()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new PreferredVersionRepository(context);

            var result = await repository.UpdateDesiredQuantityAsync(999, 1);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task UpgradeAsync_ExistingID_MovesToNewPrintingKeepingDesiredQuantity()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new PreferredVersionRepository(context);
            await repository.AddOrUpdateAsync(101, 1, desiredQuantity: 1);
            var created = (await repository.GetByCardIDAsync(1)).Single();

            var result = await repository.UpgradeAsync(created.ID, 102);

            Assert.IsTrue(result);
            var updated = (await repository.GetByCardIDAsync(1)).Single();
            Assert.AreEqual(102, updated.CatalogPrintingID);
            Assert.AreEqual(1, updated.DesiredQuantity);
        }

        [TestMethod]
        public async Task UpgradeAsync_NoSuchID_ReturnsFalse()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new PreferredVersionRepository(context);

            var result = await repository.UpgradeAsync(999, 102);

            Assert.IsFalse(result);
        }
    }
}
