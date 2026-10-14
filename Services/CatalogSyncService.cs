using System.Diagnostics.CodeAnalysis;
using CardCollector.Data.Models.Catalog;
using CardCollector.DTO;
using CardCollector.DTO.YamlYugi;
using CardCollector.DTO.YGOProDeck;
using CardCollector.Models.Catalog;
using CardCollector.Repository;
using CardCollector.Repository.Catalog;
using CardCollector.Repository.Sources;
using CardCollector.Rules.Catalog;
using Microsoft.Extensions.Options;

namespace CardCollector.Services
{
    public sealed class CatalogSyncService : ICatalogSyncService
    {
        private const int RECENT_RUNS_TO_COMPARE = 30;

        private readonly ICardSetRepository _cardSetRepository;
        private readonly ICatalogRepository _catalogRepository;
        private readonly ILogger<CatalogSyncService> _logger;
        private readonly CatalogSyncSettings _settings;
        private readonly ITCGCatalogCache _tcgCatalogCache;
        private readonly TimeProvider _timeProvider;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IYamlYugiSource _yamlYugiSource;
        private readonly IYGOProDeckSource _ygoProDeckSource;

        public CatalogSyncService(
            ICardSetRepository cardSetRepository,
            ICatalogRepository catalogRepository,
            ILogger<CatalogSyncService> logger,
            IOptions<CatalogSyncSettings> settings,
            ITCGCatalogCache tcgCatalogCache,
            IUnitOfWork unitOfWork,
            IYamlYugiSource yamlYugiSource,
            IYGOProDeckSource ygoProDeckSource,
            TimeProvider? timeProvider = null)
        {
            _cardSetRepository = cardSetRepository;
            _catalogRepository = catalogRepository;
            _logger = logger;
            _settings = settings.Value;
            _tcgCatalogCache = tcgCatalogCache;
            _unitOfWork = unitOfWork;
            _yamlYugiSource = yamlYugiSource;
            _ygoProDeckSource = ygoProDeckSource;
            _timeProvider = timeProvider ?? TimeProvider.System;
        }

        /// <summary>
        /// Judges one source's download: no records is a failure, and fewer than <paramref name="minRatio"/> of its count in
        /// the last run where it succeeded means a partial download, which is skipped (public for direct unit testing).
        /// </summary>
        public static CatalogSourceStatus EvaluateSource(int count, int? lastSucceededCount, double minRatio)
        {
            if (count == 0)
                return CatalogSourceStatus.Failed;

            return lastSucceededCount is int last && last > 0 && count < last * minRatio
                ? CatalogSourceStatus.Skipped
                : CatalogSourceStatus.Succeeded;
        }

        public async Task<CatalogSyncResult> SyncAsync(CatalogSourceMode sourceMode, CancellationToken cancellationToken = default)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var recentRuns = await _catalogRepository.GetRecentSyncRunsAsync(RECENT_RUNS_TO_COMPARE, cancellationToken).ConfigureAwait(false);

            // At start-up the sources are loaded either way (prices need the TCGPlayer cache in memory), but the catalog
            // only needs syncing when a cache was rewritten after the last successful run.
            CatalogSourceData? preloaded = null;
            if (sourceMode == CatalogSourceMode.IfStale)
            {
                try
                {
                    preloaded = await LoadSourcesAsync(sourceMode).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Loading the catalog sources before the start-up sync failed; the sync will retry and record the failure");
                }

                var lastSucceeded = recentRuns.FirstOrDefault(r => r.Status == CatalogSyncRunStatus.Succeeded);
                if (preloaded is not null
                    && CatalogSourceFreshness.IsAlreadySynced(lastSucceeded, [_yamlYugiSource.CachedAtUTC, _ygoProDeckSource.CachedAtUTC, _tcgCatalogCache.CachedAtUTC]))
                {
                    _logger.LogInformation("Catalog sync skipped: the cached sources haven't changed since run {RunID} finished at {FinishedUTC:O}", lastSucceeded!.ID, lastSucceeded.FinishedUTC);
                    return new CatalogSyncResult { Run = lastSucceeded, Skipped = true };
                }
            }

            var run = new CatalogSyncRun
            {
                DateCreated = now,
                DateModified = now,
                StartedUTC = now,
                Status = CatalogSyncRunStatus.Running
            };
            await _catalogRepository.AddSyncRunAsync(run, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Catalog sync {RunID} started ({SourceMode})", run.ID, sourceMode);

            IReadOnlyDictionary<CatalogReviewItemKind, int> openedByKind = new Dictionary<CatalogReviewItemKind, int>();
            try
            {
                openedByKind = await RunAsync(run, recentRuns, sourceMode, preloaded, now, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Catalog sync {RunID} failed", run.ID);
                _catalogRepository.DiscardChanges();
                run.Status = CatalogSyncRunStatus.Failed;
                run.Message = $"Unexpected error: {ex.Message}";
            }

            run.FinishedUTC = _timeProvider.GetUtcNow().UtcDateTime;
            run.DateModified = run.FinishedUTC.Value;
            await _catalogRepository.UpdateSyncRunAsync(run, CancellationToken.None).ConfigureAwait(false);

            _logger.LogInformation(
                "Catalog sync {RunID} finished {Status}: {CardsAdded} cards added, {CardsUpdated} updated, {PrintingsAdded} printings added, {ProductsLinked} products linked, {ItemsOpened} review items opened, {ItemsAutoResolved} resolved by source",
                run.ID, run.Status, run.CardsAdded, run.CardsUpdated, run.PrintingsAdded, run.ProductsLinked, run.ItemsOpened, run.ItemsAutoResolved);

            return new CatalogSyncResult { ItemsOpenedByKind = openedByKind, Run = run };
        }

        private IReadOnlyDictionary<CatalogReviewItemKind, int> Fail(CatalogSyncRun run, string message)
        {
            _logger.LogError("Catalog sync {RunID} failed: {Message}", run.ID, message);
            run.Status = CatalogSyncRunStatus.Failed;
            run.Message = message;
            return new Dictionary<CatalogReviewItemKind, int>();
        }

        private static int? LastSucceededCount(
            IEnumerable<CatalogSyncRun> recentRuns,
            Func<CatalogSyncRun, CatalogSourceStatus?> status,
            Func<CatalogSyncRun, int> count) =>
            recentRuns.FirstOrDefault(r => status(r) == CatalogSourceStatus.Succeeded) is { } last ? count(last) : null;

        [ExcludeFromCodeCoverage(Justification = "Thin dispatch over the source caches' own load methods; I/O, not testable logic.")]
        private async Task<CatalogSourceData> LoadSourcesAsync(CatalogSourceMode sourceMode)
        {
            if (sourceMode == CatalogSourceMode.CacheOnly)
                return new CatalogSourceData(_yamlYugiSource.ReadCached(), _ygoProDeckSource.ReadCached(), _tcgCatalogCache.GetAllPrintings());

            var force = sourceMode == CatalogSourceMode.Force;
            if (force)
                await _tcgCatalogCache.RefreshAsync().ConfigureAwait(false);
            else
                await _tcgCatalogCache.LoadIfStaleAsync().ConfigureAwait(false);
            await _cardSetRepository.LoadIfStaleAsync().ConfigureAwait(false);

            var yamlYugi = await _yamlYugiSource.LoadAsync(force).ConfigureAwait(false);
            var ygoProDeck = await _ygoProDeckSource.LoadAsync(force).ConfigureAwait(false);
            return new CatalogSourceData(yamlYugi, ygoProDeck, _tcgCatalogCache.GetAllPrintings());
        }

        /// <summary>Records a guardrail stop as its own review item, after the run's real changes were thrown away.</summary>
        private async Task RecordAnomalyAsync(CatalogSyncRun run, string summary, object payload, DateTime now, CancellationToken cancellationToken)
        {
            var existing = await _catalogRepository.GetReviewItemsAsync(cancellationToken).ConfigureAwait(false);
            var anomaly = new DetectedReviewItem
            {
                ExternalSubject = "sync:guardrail",
                Kind = CatalogReviewItemKind.SyncAnomaly,
                Payload = payload,
                Summary = summary
            };
            var reconciled = CatalogReviewItemRules.Reconcile(existing, [anomaly], new HashSet<CatalogSource>(), run.ID, now);
            _catalogRepository.AddReviewItems(reconciled.NewItems);
            await _catalogRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        private async Task<IReadOnlyDictionary<CatalogReviewItemKind, int>> RunAsync(
            CatalogSyncRun run,
            IReadOnlyList<CatalogSyncRun> recentRuns,
            CatalogSourceMode sourceMode,
            CatalogSourceData? preloaded,
            DateTime now,
            CancellationToken cancellationToken)
        {
            var (yamlCards, ygoProDeckCards, tcgRows) = preloaded ?? await LoadSourcesAsync(sourceMode).ConfigureAwait(false);
            var corrections = RarityCorrectionLoader.Load(_logger);
            var products = CatalogSourceNormalizer.FromTCGPlayer(tcgRows, corrections);

            run.YamlYugiCardCount = yamlCards.Count;
            run.YGOProDeckCardCount = ygoProDeckCards.Count;
            run.TCGPlayerProductCount = products.Count;
            run.YamlYugiStatus = EvaluateSource(yamlCards.Count, LastSucceededCount(recentRuns, r => r.YamlYugiStatus, r => r.YamlYugiCardCount), _settings.MinSourceRatio);
            run.YGOProDeckStatus = EvaluateSource(ygoProDeckCards.Count, LastSucceededCount(recentRuns, r => r.YGOProDeckStatus, r => r.YGOProDeckCardCount), _settings.MinSourceRatio);
            run.TCGPlayerStatus = EvaluateSource(products.Count, LastSucceededCount(recentRuns, r => r.TCGPlayerStatus, r => r.TCGPlayerProductCount), _settings.MinSourceRatio);

            foreach (var (source, status, count) in new[]
            {
                ("yaml-yugi", run.YamlYugiStatus, run.YamlYugiCardCount),
                ("YGOProDeck", run.YGOProDeckStatus, run.YGOProDeckCardCount),
                ("TCGPlayer", run.TCGPlayerStatus, run.TCGPlayerProductCount)
            })
            {
                if (status != CatalogSourceStatus.Succeeded)
                    _logger.LogWarning("Catalog sync {RunID}: {Source} {Status} with {Count} records; it is left out of this run", run.ID, source, status, count);
            }

            var succeeded = new HashSet<CatalogSource>();
            if (run.YamlYugiStatus == CatalogSourceStatus.Succeeded)
                succeeded.Add(CatalogSource.YamlYugi);
            if (run.YGOProDeckStatus == CatalogSourceStatus.Succeeded)
                succeeded.Add(CatalogSource.YGOProDeck);
            if (run.TCGPlayerStatus == CatalogSourceStatus.Succeeded)
                succeeded.Add(CatalogSource.TCGPlayer);

            var state = await _catalogRepository.LoadStateAsync(cancellationToken).ConfigureAwait(false);
            run.IsBootstrap = state.IsEmpty;

            if (run.IsBootstrap && !(succeeded.Contains(CatalogSource.YamlYugi) && succeeded.Contains(CatalogSource.YGOProDeck)))
                return Fail(run, "Building the catalog needs both yaml-yugi and YGOProDeck, and at least one of them returned no usable data");
            if (succeeded.Count == 0)
                return Fail(run, "No source returned usable data");

            var context = new CatalogSyncContext
            {
                IsBootstrap = run.IsBootstrap,
                NowUTC = now,
                SetReleaseDateLookup = _cardSetRepository.GetTCGDateBySetCode,
                TCGPlayerSucceeded = succeeded.Contains(CatalogSource.TCGPlayer),
                YamlYugiSucceeded = succeeded.Contains(CatalogSource.YamlYugi),
                YGOProDeckSucceeded = succeeded.Contains(CatalogSource.YGOProDeck)
            };
            var diff = new CatalogDiffResult();
            var setResolver = new CatalogSetResolver(state, context, diff);
            var tcgProducts = context.TCGPlayerSucceeded ? products : [];

            if (context.YamlYugiSucceeded || context.YGOProDeckSucceeded)
            {
                var observed = CatalogCardMatcher.Combine(
                    context.YamlYugiSucceeded ? CatalogSourceNormalizer.FromYamlYugi(yamlCards, corrections) : [],
                    context.YGOProDeckSucceeded ? CatalogSourceNormalizer.FromYGOProDeck(ygoProDeckCards, corrections) : []);
                var match = CatalogCardMatcher.Match(observed, state.Cards);
                CatalogCardDiff.Apply(state, match, tcgProducts, context, setResolver, diff);
            }

            if (context.TCGPlayerSucceeded)
                CatalogProductDiff.Apply(state, tcgProducts, context, setResolver, diff);

            CatalogNotSeenRules.Detect(state, context, diff);

            run.CardsAdded = diff.CardsAdded;
            run.CardsUpdated = diff.CardsUpdated;
            run.PrintingsAdded = diff.PrintingsAdded;
            run.ProductsIgnored = diff.ProductsIgnored;
            run.ProductsLinked = diff.ProductsLinked;
            run.ProductsUnmapped = diff.ProductsUnmapped;

            CatalogReviewReconcileResult? reconciled = null;
            try
            {
                await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    _catalogRepository.AddSets(diff.NewSets);
                    _catalogRepository.AddCards(diff.NewCards);
                    await _catalogRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                    var existingItems = await _catalogRepository.GetReviewItemsAsync(cancellationToken).ConfigureAwait(false);
                    reconciled = CatalogReviewItemRules.Reconcile(existingItems, diff.Items, succeeded, run.ID, now);
                    if (!run.IsBootstrap && reconciled.ActionableOpened > _settings.MaxNewActionableItems)
                        throw new CatalogSyncGuardrailException(reconciled);

                    _catalogRepository.AddReviewItems(reconciled.NewItems);
                    await _catalogRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }, cancellationToken).ConfigureAwait(false);
            }
            catch (CatalogSyncGuardrailException guardrail)
            {
                _catalogRepository.DiscardChanges();
                var summary = $"A sync would have opened {guardrail.Reconciled.ActionableOpened} actionable review items (limit {_settings.MaxNewActionableItems}), so none of its changes were kept";
                _logger.LogWarning("Catalog sync {RunID} aborted: {Summary}", run.ID, summary);
                await RecordAnomalyAsync(
                    run,
                    summary,
                    new { guardrail.Reconciled.ActionableOpened, Limit = _settings.MaxNewActionableItems, OpenedByKind = guardrail.Reconciled.OpenedByKind.ToDictionary(k => k.Key.ToString(), k => k.Value) },
                    now,
                    cancellationToken).ConfigureAwait(false);

                run.Status = CatalogSyncRunStatus.Aborted;
                run.Message = summary;
                return guardrail.Reconciled.OpenedByKind;
            }

            run.ItemsAutoResolved = reconciled!.AutoResolved;
            run.ItemsOpened = reconciled.OpenedByKind.Values.Sum();
            run.Status = CatalogSyncRunStatus.Succeeded;
            return reconciled.OpenedByKind;
        }
    }
}
