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
            await repository.AddAsync([101]);

            await repository.AddAsync([101, 101]);

            var result = await repository.GetAllAsync();
            Assert.AreEqual(1, result.Count);
        }

        [TestMethod]
        public async Task AddAsync_SomeAlreadyDismissed_AddsOnlyTheRest()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new DismissedNewPrintingRepository(context);
            await repository.AddAsync([101]);

            await repository.AddAsync([101, 102]);

            Assert.AreEqual(2, context.DismissedNewPrintings.Count());
        }

        [TestMethod]
        public async Task AddAsync_TwoPrintingsInOneCall_AddsBoth()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new DismissedNewPrintingRepository(context);

            await repository.AddAsync([101, 102]);

            var result = await repository.GetAllAsync();
            CollectionAssert.AreEquivalent(new[] { 101, 102 }, result.ToArray());
        }

        [TestMethod]
        public async Task AnyAsync_HasRecords_ReturnsTrue()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new DismissedNewPrintingRepository(context);
            await repository.AddAsync([101]);

            var result = await repository.AnyAsync();

            Assert.IsTrue(result);
        }

        [TestMethod]
        public async Task AnyAsync_NoRecords_ReturnsFalse()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new DismissedNewPrintingRepository(context);

            var result = await repository.AnyAsync();

            Assert.IsFalse(result);
        }
    }
}
