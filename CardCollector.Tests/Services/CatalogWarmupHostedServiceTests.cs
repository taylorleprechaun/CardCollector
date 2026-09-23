using CardCollector.Repository;
using CardCollector.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;

namespace CardCollector.Tests.Services
{
    [TestClass]
    public sealed class CatalogWarmupHostedServiceTests
    {
        private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

        [TestMethod]
        public async Task StopAsync_AppNeverStarted_CompletesImmediately()
        {
            var context = CreateService();
            await context.Service.StartAsync(CancellationToken.None);

            var stopping = context.Service.StopAsync(CancellationToken.None);

            Assert.IsTrue(stopping.IsCompletedSuccessfully);
            await stopping;
        }

        [TestMethod]
        public async Task StopAsync_ShutdownTimeoutReached_ReturnsWithoutWaitingForTheWarmup()
        {
            var context = CreateService();
            await context.Service.StartAsync(CancellationToken.None);
            context.Started.Cancel();
            using var timedOut = new CancellationTokenSource();
            timedOut.Cancel();

            await context.Service.StopAsync(timedOut.Token).WaitAsync(TestTimeout);

            Assert.IsFalse(context.CatalogLoad.Task.IsCompleted);
        }

        [TestMethod]
        public async Task StopAsync_WarmupInProgress_WaitsForItToFinish()
        {
            var context = CreateService();
            await context.Service.StartAsync(CancellationToken.None);
            context.Started.Cancel();

            var stopping = context.Service.StopAsync(CancellationToken.None);
            var finishedBeforeWarmup = stopping.IsCompleted;
            context.CatalogLoad.SetResult();
            await stopping.WaitAsync(TestTimeout);

            Assert.IsFalse(finishedBeforeWarmup);
        }

        /// <summary>
        /// The catalog load is held open until the test completes it, so the warm-up stays in progress after
        /// <see cref="WarmupTestContext.Started"/> fires. Every other load finishes at once.
        /// </summary>
        private static WarmupTestContext CreateService()
        {
            var started = new CancellationTokenSource();
            var stopping = new CancellationTokenSource();
            var lifetime = new Mock<IHostApplicationLifetime>();
            lifetime.Setup(l => l.ApplicationStarted).Returns(started.Token);
            lifetime.Setup(l => l.ApplicationStopping).Returns(stopping.Token);

            var catalogLoad = new TaskCompletionSource();
            var catalog = new Mock<ITCGCatalogCache>();
            catalog.Setup(c => c.LoadIfStaleAsync()).Returns(catalogLoad.Task);

            var banlist = new Mock<IBanlistRepository>();
            banlist.Setup(b => b.LoadIfStaleAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            var cardData = new Mock<ICardDataRepository>();
            cardData.Setup(c => c.LoadIfStaleAsync()).Returns(Task.CompletedTask);
            var cardSets = new Mock<ICardSetRepository>();
            cardSets.Setup(c => c.LoadIfStaleAsync()).Returns(Task.CompletedTask);

            var service = new CatalogWarmupHostedService(
                banlist.Object,
                cardData.Object,
                cardSets.Object,
                lifetime.Object,
                new Mock<ILogger<CatalogWarmupHostedService>>().Object,
                new Mock<IPricingDataCache>().Object,
                catalog.Object);

            return new WarmupTestContext(service, started, catalogLoad);
        }

        private sealed record WarmupTestContext(CatalogWarmupHostedService Service, CancellationTokenSource Started, TaskCompletionSource CatalogLoad);
    }
}
