using CardCollector.Repository;
using CardCollector.Tests.TestHelpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CardCollector.Tests.Repository
{
    [TestClass]
    public sealed class DismissedNewPrintingRepositoryTests
    {
        [TestMethod]
        public async Task AddAsync_AlreadyDismissed_DoesNotAddDuplicate()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new DismissedNewPrintingRepository(context);
            await repository.AddAsync(1, "LOB-EN001", "Ultra Rare");

            await repository.AddAsync(1, "LOB-EN001", "Ultra Rare");

            var result = await repository.GetAllAsync();
            Assert.AreEqual(1, result.Count);
        }

        [TestMethod]
        public async Task AddAsync_ThenGetAllAsync_ContainsRecord()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new DismissedNewPrintingRepository(context);

            await repository.AddAsync(1, "LOB-EN001", "Ultra Rare");

            var result = await repository.GetAllAsync();
            Assert.IsTrue(result.Contains((1, "LOB-EN001", "Ultra Rare")));
        }
        [TestMethod]
        public async Task AnyAsync_HasRecords_ReturnsTrue()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new DismissedNewPrintingRepository(context);
            await repository.AddAsync(1, "LOB-EN001", "Ultra Rare");

            Assert.IsTrue(await repository.AnyAsync());
        }

        [TestMethod]
        public async Task AnyAsync_NoRecords_ReturnsFalse()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new DismissedNewPrintingRepository(context);

            Assert.IsFalse(await repository.AnyAsync());
        }
    }
}
