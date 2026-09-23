using System.Diagnostics.CodeAnalysis;
using CardCollector.Repository;

namespace CardCollector.Services
{
    /// <summary>
    /// Refreshes stale catalog/card/set caches in the background after startup, so a cold cache never
    /// blocks Kestrel from accepting connections. Runs once per process lifetime.
    /// </summary>
    public sealed class CatalogWarmupHostedService : IHostedService
    {
        private readonly IBanlistRepository _banlistRepository;
        private readonly ICardDataRepository _cardDataRepository;
        private readonly ICardSetRepository _cardSetRepository;
        private readonly IHostApplicationLifetime _lifetime;
        private readonly ILogger<CatalogWarmupHostedService> _logger;
        private readonly IPricingDataCache _pricingDataCache;
        private readonly ITCGCatalogCache _tcgCatalogCache;
        private Task _warmupTask = Task.CompletedTask;

        public CatalogWarmupHostedService(
            IBanlistRepository banlistRepository,
            ICardDataRepository cardDataRepository,
            ICardSetRepository cardSetRepository,
            IHostApplicationLifetime lifetime,
            ILogger<CatalogWarmupHostedService> logger,
            IPricingDataCache pricingDataCache,
            ITCGCatalogCache tcgCatalogCache)
        {
            _banlistRepository = banlistRepository;
            _cardDataRepository = cardDataRepository;
            _cardSetRepository = cardSetRepository;
            _lifetime = lifetime;
            _logger = logger;
            _pricingDataCache = pricingDataCache;
            _tcgCatalogCache = tcgCatalogCache;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _lifetime.ApplicationStarted.Register(() => _warmupTask = RunWarmupAsync());
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        [ExcludeFromCodeCoverage(Justification = "Orchestrates real singleton I/O warm-up; a mocked test would only re-assert mock setup, not real behavior — same rationale as PriceRefreshBackgroundService.RunNightlyRefreshAsync.")]
        private async Task RunWarmupAsync()
        {
            try
            {
                _logger.LogInformation("Startup cache warm-up: beginning background refresh of stale/missing caches");

                var catalogTask = _tcgCatalogCache.LoadIfStaleAsync();
                var setTask = _cardSetRepository.LoadIfStaleAsync();
                var banlistTask = _banlistRepository.LoadIfStaleAsync();

                await catalogTask.ConfigureAwait(false);
                await _cardDataRepository.LoadIfStaleAsync().ConfigureAwait(false);
                _pricingDataCache.RebuildIndex();

                await setTask.ConfigureAwait(false);
                await banlistTask.ConfigureAwait(false);

                _logger.LogInformation("Startup cache warm-up: complete");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Startup cache warm-up failed; site continues serving with whatever cache data was available at startup, and will retry at the next nightly refresh");
            }
        }
    }
}
