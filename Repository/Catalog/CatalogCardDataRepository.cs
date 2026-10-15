using CardCollector.DTO;
using CardCollector.Models.Catalog;
using CardCollector.Rules.Catalog;
using CardCollector.Services;
using System.Diagnostics.CodeAnalysis;

namespace CardCollector.Repository.Catalog
{
    /// <summary>
    /// Serves card data from the catalog database, held in memory as one <see cref="CatalogReadModel"/> that each reload
    /// replaces whole, so readers never see a half-built model.
    /// </summary>
    public sealed class CatalogCardDataRepository : ICardDataRepository
    {
        private readonly ILogger<CatalogCardDataRepository> _logger;
        private volatile CatalogReadModel _model = CatalogReadModel.Empty;
        private readonly object _queueGate = new();
        /// <summary>Runs reloads one at a time, so a reload that started later (and read newer data) always finishes last.</summary>
        private readonly SemaphoreSlim _reloadLock = new(1, 1);
        private bool _reloadPending;
        private bool _reloadQueued;
        private readonly IServiceScopeFactory _scopeFactory;
        /// <summary>Lets only one sync run at a time (the startup warm-up can overlap the nightly refresh).</summary>
        private readonly SemaphoreSlim _syncLock = new(1, 1);

        public CatalogCardDataRepository(ILogger<CatalogCardDataRepository> logger, IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        public IReadOnlyList<string> AllRarityNames => _model.AllRarityNames;

        public IReadOnlyList<string> DistinctRarityNames => _model.DistinctRarityNames;

        public IReadOnlyList<string> DistinctSetNames => _model.DistinctSetNames;

        public IEnumerable<Card> GetAllCards() => _model.Cards;

        public IEnumerable<Card> GetBrowseableCards() => _model.BrowseableCards;

        public Card? GetCardByID(int cardID) => _model.CardsByID.GetValueOrDefault(cardID);

        public Card? GetCardByPasscode(int passcode)
        {
            var model = _model;
            return model.PasscodeAliases.TryGetValue(passcode, out var cardID) ? model.CardsByID.GetValueOrDefault(cardID) : null;
        }

        public int? GetCardIDByPrintingID(int printingID) =>
            _model.CardIDsByPrintingID.TryGetValue(printingID, out var cardID) ? cardID : null;

        public IReadOnlyList<Card> GetCardsByName(string name) =>
            _model.CardsByName.TryGetValue(name, out var cards) ? cards : [];

        public IReadOnlyDictionary<int, int> GetPasscodeAliases() => _model.PasscodeAliases;

        public CatalogPrintingRef? GetPrintingByID(int printingID) => _model.PrintingsByID.GetValueOrDefault(printingID);

        public IReadOnlyList<CatalogPrintingRef> GetPrintingsBySetCode(string setCode) =>
            _model.PrintingsBySetCode.TryGetValue(setCode, out var printings) ? printings : [];

        public IReadOnlyDictionary<string, string> GetSetNamesByCode() => _model.SetNamesByCode;

        public string? GetSetPrefixByName(string canonicalName) =>
            _model.SetPrefixByName.TryGetValue(canonicalName, out var prefix) ? prefix : null;

        public Task LoadIfStaleAsync() => SyncAndReloadAsync(CatalogSourceMode.IfStale);

        [ExcludeFromCodeCoverage(Justification = "Starts a background reload through a DI scope; the model itself is built by the tested CatalogReadModelBuilder.")]
        public void QueueReload()
        {
            lock (_queueGate)
            {
                if (_reloadQueued)
                {
                    _reloadPending = true;
                    return;
                }

                _reloadQueued = true;
            }

            _ = Task.Run(RunQueuedReloadsAsync);
        }

        public Task RefreshAsync() => SyncAndReloadAsync(CatalogSourceMode.Force);

        [ExcludeFromCodeCoverage(Justification = "Loads through a DI scope and swaps the model; the model itself is built by the tested CatalogReadModelBuilder.")]
        public async Task ReloadAsync()
        {
            await _reloadLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var catalogRepository = scope.ServiceProvider.GetRequiredService<ICatalogRepository>();
                var state = await catalogRepository.LoadSnapshotAsync().ConfigureAwait(false);

                _model = CatalogReadModelBuilder.Build(state);
                _logger.LogInformation(
                    "Card data loaded from the catalog: {CardCount} cards, {BrowseableCount} browseable",
                    _model.Cards.Count, _model.BrowseableCards.Count);
            }
            finally
            {
                _reloadLock.Release();
            }
        }

        /// <summary>
        /// Reloads until no request is left: a request that arrives during a reload may have saved changes that reload
        /// already missed, so it gets one more.
        /// </summary>
        [ExcludeFromCodeCoverage(Justification = "Background loop around ReloadAsync; see QueueReload.")]
        private async Task RunQueuedReloadsAsync()
        {
            while (true)
            {
                try
                {
                    await ReloadAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // The edit is saved; the card data catches up at the next reload (the next edit, nightly, or on restart).
                    _logger.LogError(ex, "Reloading card data in the background failed");
                }

                lock (_queueGate)
                {
                    if (!_reloadPending)
                    {
                        _reloadQueued = false;
                        return;
                    }

                    _reloadPending = false;
                }
            }
        }

        [ExcludeFromCodeCoverage(Justification = "Runs the sync through a DI scope; the sync is tested directly.")]
        private async Task SyncAndReloadAsync(CatalogSourceMode sourceMode)
        {
            await _syncLock.WaitAsync().ConfigureAwait(false);
            try
            {
                CatalogSyncResult result;
                await using (var scope = _scopeFactory.CreateAsyncScope())
                {
                    var syncService = scope.ServiceProvider.GetRequiredService<ICatalogSyncService>();
                    result = await syncService.SyncAsync(sourceMode).ConfigureAwait(false);
                }

                // A skipped sync changed nothing, so the model loaded at start-up (or by the last edit) is still current.
                if (result.Skipped && !ReferenceEquals(_model, CatalogReadModel.Empty))
                    return;

                await ReloadAsync().ConfigureAwait(false);
            }
            finally
            {
                _syncLock.Release();
            }
        }
    }
}
