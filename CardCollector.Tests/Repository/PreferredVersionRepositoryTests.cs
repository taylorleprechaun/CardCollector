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
            await repository.AddOrUpdateAsync(1, 10, "LOB-EN001", "Ultra Rare");

            await repository.AddOrUpdateAsync(1, 10, "LOB-EN002", "Secret Rare");

            var result = await repository.GetByCardIDAsync(1);
            Assert.AreEqual(2, result.Count);
            CollectionAssert.AreEquivalent(new[] { "LOB-EN001", "LOB-EN002" }, result.Select(r => r.SetCode).ToArray());
        }

        [TestMethod]
        public async Task AddOrUpdateAsync_NoExistingRecord_CreatesNew()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new PreferredVersionRepository(context);

            await repository.AddOrUpdateAsync(1, 10, "LOB-EN001", "Ultra Rare");

            var result = await repository.GetByCardIDAsync(1);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("LOB-EN001", result[0].SetCode);
        }

        [TestMethod]
        public async Task AddOrUpdateAsync_NoExistingRecordDesiredQuantityGiven_UsesGivenValue()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new PreferredVersionRepository(context);

            await repository.AddOrUpdateAsync(1, 10, "LOB-EN001", desiredQuantity: 1);

            var result = (await repository.GetByCardIDAsync(1)).Single();
            Assert.AreEqual(1, result.DesiredQuantity);
        }

        [TestMethod]
        public async Task AddOrUpdateAsync_NoExistingRecordNoDesiredQuantityGiven_DefaultsToThree()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new PreferredVersionRepository(context);

            await repository.AddOrUpdateAsync(1, 10, "LOB-EN001");

            var result = (await repository.GetByCardIDAsync(1)).Single();
            Assert.AreEqual(3, result.DesiredQuantity);
        }

        [TestMethod]
        public async Task AddOrUpdateAsync_NoExistingRecordShortPrintRarityName_NormalizesToCommon()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new PreferredVersionRepository(context);

            await repository.AddOrUpdateAsync(1, 10, "LOB-EN001", "Short Print");

            var result = (await repository.GetByCardIDAsync(1)).Single();
            Assert.AreEqual("Common", result.RarityName);
        }

        [TestMethod]
        public async Task AddOrUpdateAsync_SameExactPrintingDesiredQuantityGiven_OverwritesValue()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new PreferredVersionRepository(context);
            await repository.AddOrUpdateAsync(1, 10, "LOB-EN001", "Ultra Rare", desiredQuantity: 1);

            await repository.AddOrUpdateAsync(1, 10, "LOB-EN001", "Ultra Rare", desiredQuantity: 2);

            var result = (await repository.GetByCardIDAsync(1)).Single();
            Assert.AreEqual(2, result.DesiredQuantity);
        }

        [TestMethod]
        public async Task AddOrUpdateAsync_SameExactPrintingNoDesiredQuantityGiven_PreservesExistingValue()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new PreferredVersionRepository(context);
            await repository.AddOrUpdateAsync(1, 10, "LOB-EN001", "Ultra Rare", desiredQuantity: 1);

            await repository.AddOrUpdateAsync(1, 20, "LOB-EN001", "Ultra Rare");

            var result = (await repository.GetByCardIDAsync(1)).Single();
            Assert.AreEqual(1, result.DesiredQuantity);
            Assert.AreEqual(20, result.ImageID);
            Assert.AreEqual(1, (await repository.GetAllAsync()).Count());
        }

        [TestMethod]
        public async Task AddOrUpdateAsync_ShortPrintRarityNameVariantsNormalizeToSameMatchKey_UpdatesInPlace()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new PreferredVersionRepository(context);
            await repository.AddOrUpdateAsync(1, 10, "LOB-EN001", "Short Print");

            await repository.AddOrUpdateAsync(1, 20, "LOB-EN001", "Super Short Print");

            var result = (await repository.GetByCardIDAsync(1)).Single();
            Assert.AreEqual("Common", result.RarityName);
            Assert.AreEqual(20, result.ImageID);
        }
        [TestMethod]
        public async Task DeleteAsync_ExistingID_RemovesRecord()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new PreferredVersionRepository(context);
            await repository.AddOrUpdateAsync(1, 10, "LOB-EN001");
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
        public async Task GetByImageIDsAsync_EmptyInput_ReturnsEmpty()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new PreferredVersionRepository(context);

            var result = await repository.GetByImageIDsAsync([]);

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public async Task GetByImageIDsAsync_MatchingIDs_ReturnsKeyedByImageID()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new PreferredVersionRepository(context);
            await repository.AddOrUpdateAsync(1, 10, "LOB-EN001");

            var result = await repository.GetByImageIDsAsync([10, 999]);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("LOB-EN001", result[10].Single().SetCode);
        }

        [TestMethod]
        public async Task GetByImageIDsAsync_TwoTrackedPrintingsShareImageID_BothReturnedForThatKey()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new PreferredVersionRepository(context);
            await repository.AddOrUpdateAsync(1, 10, "SUDA-EN001", "Secret Rare");
            await repository.AddOrUpdateAsync(1, 10, "RA04-EN001", "Quarter Century Secret Rare");

            var result = await repository.GetByImageIDsAsync([10]);

            Assert.AreEqual(2, result[10].Count);
            CollectionAssert.AreEquivalent(new[] { "SUDA-EN001", "RA04-EN001" }, result[10].Select(pv => pv.SetCode).ToArray());
        }

        [TestMethod]
        public async Task GetPreferredCardIDsAsync_ReturnsDistinctCardIDs()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new PreferredVersionRepository(context);
            await repository.AddOrUpdateAsync(1, 10, "LOB-EN001");
            await repository.AddOrUpdateAsync(2, 20, "LOB-EN002");

            var result = await repository.GetPreferredCardIDsAsync();

            CollectionAssert.AreEquivalent(new[] { 1, 2 }, result.ToArray());
        }

        [TestMethod]
        public async Task UpdateDesiredQuantityAsync_ExistingID_UpdatesAndReturnsTrue()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new PreferredVersionRepository(context);
            await repository.AddOrUpdateAsync(1, 10, "LOB-EN001");
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
        public async Task UpgradeAsync_ExistingID_UpdatesSetAndRarityPreservingDesiredQuantity()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new PreferredVersionRepository(context);
            await repository.AddOrUpdateAsync(1, 10, "LOB-EN001", "Ultra Rare", desiredQuantity: 1);
            var created = (await repository.GetByCardIDAsync(1)).Single();

            var result = await repository.UpgradeAsync(created.ID, "LOB-EN002", "Secret Rare");

            Assert.IsTrue(result);
            var updated = (await repository.GetByCardIDAsync(1)).Single();
            Assert.AreEqual("LOB-EN002", updated.SetCode);
            Assert.AreEqual("Secret Rare", updated.RarityName);
            Assert.AreEqual(1, updated.DesiredQuantity);
        }

        [TestMethod]
        public async Task UpgradeAsync_NoSuchID_ReturnsFalse()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new PreferredVersionRepository(context);

            var result = await repository.UpgradeAsync(999, "LOB-EN002", "Secret Rare");

            Assert.IsFalse(result);
        }
    }
}
