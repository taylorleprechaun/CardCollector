using CardCollector.Data.Models.Catalog;
using CardCollector.Models.Catalog;
using CardCollector.Models.Legacy;
using CardCollector.Repository.Catalog;
using CardCollector.Repository.Legacy;
using CardCollector.Repository.Sources;
using CardCollector.Rules.Catalog;

namespace CardCollector.Services
{
    public sealed class LegacyMigrationService : ILegacyMigrationService
    {
        /// <summary>
        /// Tables copied as they are, parents before children. None of them refers to a card, except that value snapshots
        /// are linked to their catalog card once the collection entries are in (they keep their own copy of the card's name,
        /// set, and rarity for display).
        /// </summary>
        private static readonly IReadOnlyList<string> UnmappedTables =
        [
            "Formats",
            "FormatStrategies",
            "Events",
            "Matches",
            "Decks",
            "CollectionValueSnapshots",
            "WishlistValueSnapshots",
            "CollectionEntryValueSnapshots"
        ];

        private readonly ICatalogRepository _catalogRepository;
        private readonly ICatalogSyncService _catalogSyncService;
        private readonly ILegacyImportRepository _importRepository;
        private readonly ILegacyDatabaseReader _legacyReader;
        private readonly ILogger<LegacyMigrationService> _logger;
        private readonly TimeProvider _timeProvider;

        public LegacyMigrationService(
            ICatalogRepository catalogRepository,
            ICatalogSyncService catalogSyncService,
            ILegacyImportRepository importRepository,
            ILegacyDatabaseReader legacyReader,
            ILogger<LegacyMigrationService> logger,
            TimeProvider? timeProvider = null)
        {
            _catalogRepository = catalogRepository;
            _catalogSyncService = catalogSyncService;
            _importRepository = importRepository;
            _legacyReader = legacyReader;
            _logger = logger;
            _timeProvider = timeProvider ?? TimeProvider.System;
        }

        public async Task<LegacyMigrationReport> MigrateAsync(string legacyDatabasePath, CatalogSourceMode sourceMode, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(legacyDatabasePath)) throw new ArgumentException("A legacy database path is required.", nameof(legacyDatabasePath));
            if (!File.Exists(legacyDatabasePath)) throw new FileNotFoundException("The legacy database was not found.", legacyDatabasePath);

            var report = new LegacyMigrationReport { LegacyDatabasePath = legacyDatabasePath, StartedUTC = _timeProvider.GetUtcNow().UtcDateTime };
            _logger.LogInformation("Legacy migration started from {LegacyDatabasePath}", legacyDatabasePath);

            await _importRepository.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
            var sync = await _catalogSyncService.SyncAsync(sourceMode, cancellationToken).ConfigureAwait(false);
            report.CatalogSync = sync.Run;
            if (sync.Run.Status != CatalogSyncRunStatus.Succeeded)
                throw new InvalidOperationException($"The catalog could not be built ({sync.Run.Status}): {sync.Run.Message}");

            var state = await _catalogRepository.LoadSnapshotAsync(cancellationToken).ConfigureAwait(false);
            var index = LegacyRowMapper.BuildIndex(state, RarityCorrectionLoader.Load(_logger));
            var legacyTables = await _legacyReader.GetTableNamesAsync(legacyDatabasePath, cancellationToken).ConfigureAwait(false);
            var items = new List<DetectedReviewItem>();

            foreach (var table in UnmappedTables.Where(legacyTables.Contains))
            {
                var copied = await _importRepository.CopyTableAsync(legacyDatabasePath, table, cancellationToken).ConfigureAwait(false);
                report.Tables.Add(new LegacyTableReport { Read = copied, Table = table, Written = copied });
            }

            var importer = new LegacyRowImporter(index, items, report);
            await ImportAsync(legacyDatabasePath, legacyTables, "CollectionEntries", importer.ImportCollectionEntries, cancellationToken).ConfigureAwait(false);
            await ImportAsync(legacyDatabasePath, legacyTables, "PreferredVersions", importer.ImportPreferredVersions, cancellationToken).ConfigureAwait(false);
            await ImportAsync(legacyDatabasePath, legacyTables, "CheckedOutCards", importer.ImportCheckedOutCards, cancellationToken).ConfigureAwait(false);
            await ImportAsync(legacyDatabasePath, legacyTables, "PendingOrderLines", importer.ImportPendingOrderLines, cancellationToken).ConfigureAwait(false);
            await ImportAsync(legacyDatabasePath, legacyTables, "DismissedNewPrintings", importer.ImportDismissedNewPrintings, cancellationToken).ConfigureAwait(false);
            await ImportAsync(legacyDatabasePath, legacyTables, "IgnoredCards", importer.ImportIgnoredCards, cancellationToken).ConfigureAwait(false);
            await ImportAsync(legacyDatabasePath, legacyTables, "DeckCards", importer.ImportDeckCards, cancellationToken).ConfigureAwait(false);
            report.SnapshotCardFill = await _importRepository.FillSnapshotCardIDsAsync(cancellationToken).ConfigureAwait(false);

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var existingItems = await _catalogRepository.GetReviewItemsAsync(cancellationToken).ConfigureAwait(false);
            var reconciled = CatalogReviewItemRules.Reconcile(existingItems, items, new HashSet<CatalogSource>(), sync.Run.ID, now);
            _catalogRepository.AddReviewItems(reconciled.NewItems);
            await _catalogRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            report.FinishedUTC = _timeProvider.GetUtcNow().UtcDateTime;
            _logger.LogInformation(
                "Legacy migration finished: {RowsWritten} rows written, {RowsUnmapped} unmapped rows sent to the review inbox",
                report.Tables.Sum(t => t.Written), report.UnmappedRows.Count);
            return report;
        }

        private async Task ImportAsync<TEntity>(
            string legacyDatabasePath,
            IReadOnlySet<string> legacyTables,
            string table,
            Func<IReadOnlyList<LegacyRow>, IReadOnlyList<TEntity>> import,
            CancellationToken cancellationToken)
            where TEntity : class
        {
            if (!legacyTables.Contains(table))
                return;

            var rows = await _legacyReader.ReadTableAsync(legacyDatabasePath, table, cancellationToken).ConfigureAwait(false);
            _importRepository.AddRange(import(rows));
            await _importRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
