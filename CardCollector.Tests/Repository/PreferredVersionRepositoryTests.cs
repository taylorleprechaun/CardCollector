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
        public async Task AddOrUpdateAsync_ExistingRecordForCard_UpdatesInPlace()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new PreferredVersionRepository(context);
            await repository.AddOrUpdateAsync(1, 10, "LOB-EN001", "Ultra Rare");

            await repository.AddOrUpdateAsync(1, 20, "LOB-EN002", "Secret Rare");

            var result = await repository.GetByCardIDAsync(1);
            Assert.AreEqual(20, result!.ImageID);
            Assert.AreEqual("LOB-EN002", result.SetCode);
            Assert.AreEqual(1, (await repository.GetAllAsync()).Count());
        }

        [TestMethod]
        public async Task AddOrUpdateAsync_NoExistingRecord_CreatesNew()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new PreferredVersionRepository(context);

            await repository.AddOrUpdateAsync(1, 10, "LOB-EN001", "Ultra Rare");

            var result = await repository.GetByCardIDAsync(1);
            Assert.IsNotNull(result);
            Assert.AreEqual("LOB-EN001", result!.SetCode);
        }
        [TestMethod]
        public async Task DeleteAsync_ExistingImageID_RemovesRecord()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new PreferredVersionRepository(context);
            await repository.AddOrUpdateAsync(1, 10, "LOB-EN001");

            await repository.DeleteAsync(10);

            Assert.IsNull(await repository.GetByCardIDAsync(1));
        }

        [TestMethod]
        public async Task DeleteAsync_NoSuchImageID_DoesNotThrow()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new PreferredVersionRepository(context);

            await repository.DeleteAsync(999);
        }

        [TestMethod]
        public async Task GetByCardIDAsync_NoRecord_ReturnsNull()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new PreferredVersionRepository(context);

            var result = await repository.GetByCardIDAsync(999);

            Assert.IsNull(result);
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
            Assert.AreEqual("LOB-EN001", result[10].SetCode);
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
    }
}
