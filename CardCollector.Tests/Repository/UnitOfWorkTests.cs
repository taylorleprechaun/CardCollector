using CardCollector.Repository;
using CardCollector.Tests.TestHelpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CardCollector.Tests.Repository
{
    [TestClass]
    public sealed class UnitOfWorkTests
    {
        [TestMethod]
        public async Task ExecuteInTransactionAsync_OperationSucceeds_CommitsAndRunsOperation()
        {
            using var context = InMemoryDbContextFactory.Create();
            var unitOfWork = new UnitOfWork(context);
            var ran = false;

            await unitOfWork.ExecuteInTransactionAsync(() => { ran = true; return Task.CompletedTask; });

            Assert.IsTrue(ran);
        }

        [TestMethod]
        public async Task ExecuteInTransactionAsync_OperationThrows_PropagatesException()
        {
            using var context = InMemoryDbContextFactory.Create();
            var unitOfWork = new UnitOfWork(context);

            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
                unitOfWork.ExecuteInTransactionAsync(() => throw new InvalidOperationException("boom")));
        }
    }
}
