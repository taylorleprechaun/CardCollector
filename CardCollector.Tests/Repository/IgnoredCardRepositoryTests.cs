using CardCollector.Repository;
using CardCollector.Tests.TestHelpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CardCollector.Tests.Repository
{
    [TestClass]
    public sealed class IgnoredCardRepositoryTests
    {
        [TestMethod]
        public async Task AddAsync_AlreadyIgnored_DoesNotAddDuplicate()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new IgnoredCardRepository(context);
            await repository.AddAsync(1);

            await repository.AddAsync(1);

            Assert.AreEqual(1, (await repository.GetAllAsync()).Count);
        }

        [TestMethod]
        public async Task AddAsync_ThenIsIgnoredAsync_ReturnsTrue()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new IgnoredCardRepository(context);

            await repository.AddAsync(1);

            Assert.IsTrue(await repository.IsIgnoredAsync(1));
        }
        [TestMethod]
        public async Task GetIgnoredCardIDsAsync_ReturnsSetOfIDs()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new IgnoredCardRepository(context);
            await repository.AddAsync(1);
            await repository.AddAsync(2);

            var result = await repository.GetIgnoredCardIDsAsync();

            CollectionAssert.AreEquivalent(new[] { 1, 2 }, result.ToArray());
        }

        [TestMethod]
        public async Task IsIgnoredAsync_NotIgnored_ReturnsFalse()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new IgnoredCardRepository(context);

            Assert.IsFalse(await repository.IsIgnoredAsync(999));
        }

        [TestMethod]
        public async Task RemoveAsync_ExistingRecord_ClearsIgnoredStatus()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new IgnoredCardRepository(context);
            await repository.AddAsync(1);

            await repository.RemoveAsync(1);

            Assert.IsFalse(await repository.IsIgnoredAsync(1));
        }

        [TestMethod]
        public async Task RemoveAsync_NoSuchRecord_DoesNotThrow()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new IgnoredCardRepository(context);

            await repository.RemoveAsync(999);
        }
    }
}
