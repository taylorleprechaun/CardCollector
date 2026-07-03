using CardCollector.Repository;

namespace CardCollector.Services
{
    public sealed class PriceRefreshBackgroundService : BackgroundService
    {
        private static readonly TimeZoneInfo EasternTz =
            TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

        private readonly ILogger<PriceRefreshBackgroundService> _logger;
        private readonly IPricingDataCache _pricingDataCache;
        private readonly IServiceScopeFactory _scopeFactory;

        public PriceRefreshBackgroundService(
            ILogger<PriceRefreshBackgroundService> logger,
            IPricingDataCache pricingDataCache,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _pricingDataCache = pricingDataCache;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var delay = GetDelayUntilNextMidnightEastern();
                _logger.LogInformation(
                    "PriceRefreshBackgroundService: next run in {Hours:F1} hours",
                    delay.TotalHours);

                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                if (stoppingToken.IsCancellationRequested)
                    return;

                await RunNightlyRefreshAsync(stoppingToken);
            }
        }

        private async Task RunNightlyRefreshAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("PriceRefreshBackgroundService: starting nightly price refresh");
            try
            {
                await _pricingDataCache.RefreshAsync();

                await using var scope = _scopeFactory.CreateAsyncScope();
                var cardService = scope.ServiceProvider.GetRequiredService<ICardService>();
                var collectionValueRepo = scope.ServiceProvider.GetRequiredService<ICollectionValueRepository>();
                var collectionEntryValueRepo = scope.ServiceProvider.GetRequiredService<ICollectionEntryValueRepository>();

                await cardService.CalculateCurrentMarketValueAsync();

                _logger.LogInformation("PriceRefreshBackgroundService: price refresh complete, pruning snapshots");

                await collectionValueRepo.PruneSnapshotsAsync();
                await collectionEntryValueRepo.PruneSnapshotsAsync();

                _logger.LogInformation("PriceRefreshBackgroundService: pruning complete");
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex,
                    "PriceRefreshBackgroundService: nightly refresh failed; will retry at next midnight");
            }
        }

        private static TimeSpan GetDelayUntilNextMidnightEastern()
        {
            var nowUtc = DateTime.UtcNow;
            var nowEastern = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, EasternTz);
            var nextMidnightEastern = nowEastern.Date.AddDays(1);
            var nextMidnightUtc = TimeZoneInfo.ConvertTimeToUtc(nextMidnightEastern, EasternTz);
            var delay = nextMidnightUtc - nowUtc;
            return delay <= TimeSpan.Zero ? TimeSpan.FromSeconds(5) : delay;
        }
    }
}
