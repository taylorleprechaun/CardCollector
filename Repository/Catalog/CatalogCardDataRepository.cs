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
        private readonly IServiceScopeFactory _scopeFactory;
        /// <summary>Lets only one sync run at a time (the startup warm-up can overlap the nightly refresh).</summary>
        private readonly SemaphoreSlim _syncLock = new(1, 1);
        private volatile CatalogReadModel _model = CatalogReadModel.Empty;

        public CatalogCardDataRepository(ILogger<CatalogCardDataRepository> logger, IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

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

        public IReadOnlyDictionary<int, int> GetPasscodeAliases() => _model.PasscodeAliases;

        public CatalogPrintingRef? GetPrintingByID(int printingID) => _model.PrintingsByID.GetValueOrDefault(printingID);

        public IReadOnlyDictionary<string, string> GetSetNamesByCode() => _model.SetNamesByCode;

        public string? GetSetPrefixByName(string canonicalName) =>
            _model.SetPrefixByName.TryGetValue(canonicalName, out var prefix) ? prefix : null;

        public Task LoadIfStaleAsync() => SyncAndReloadAsync(CatalogSourceMode.IfStale);

        public Task RefreshAsync() => SyncAndReloadAsync(CatalogSourceMode.Force);

        [ExcludeFromCodeCoverage(Justification = "Loads through a DI scope and swaps the model; the model itself is built by the tested CatalogReadModelBuilder.")]
        public async Task ReloadAsync()
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var catalogRepository = scope.ServiceProvider.GetRequiredService<ICatalogRepository>();
            var state = await catalogRepository.LoadSnapshotAsync().ConfigureAwait(false);

            _model = CatalogReadModelBuilder.Build(state);
            _logger.LogInformation(
                "Card data loaded from the catalog: {CardCount} cards, {BrowseableCount} browseable",
                _model.Cards.Count, _model.BrowseableCards.Count);
        }

        [ExcludeFromCodeCoverage(Justification = "Runs the sync through a DI scope; the sync is tested directly.")]
        private async Task SyncAndReloadAsync(CatalogSourceMode sourceMode)
        {
            await _syncLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await using (var scope = _scopeFactory.CreateAsyncScope())
                {
                    var syncService = scope.ServiceProvider.GetRequiredService<ICatalogSyncService>();
                    await syncService.SyncAsync(sourceMode).ConfigureAwait(false);
                }

                await ReloadAsync().ConfigureAwait(false);
            }
            finally
            {
                _syncLock.Release();
            }
        }
    }
}
