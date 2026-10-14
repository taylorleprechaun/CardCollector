using System.Diagnostics.CodeAnalysis;
using CardCollector.Data.Models.Catalog;
using CardCollector.Models.Catalog;
using CardCollector.Services;

namespace CardCollector
{
    /// <summary>
    /// The maintenance commands the app runs instead of starting the web server:
    /// <list type="bullet">
    /// <item><c>catalog-sync --db &lt;path&gt; [--offline]</c> builds or updates a catalog database.</item>
    /// <item><c>migrate-legacy --from &lt;legacy path&gt; --to &lt;new path&gt; [--offline] [--overwrite]</c> builds a new
    /// database from a legacy one.</item>
    /// </list>
    /// </summary>
    public sealed class CatalogCommandLine
    {
        public const string CATALOG_SYNC = "catalog-sync";
        public const string MIGRATE_LEGACY = "migrate-legacy";

        private CatalogCommandLine()
        {
        }

        /// <summary>The command name: <see cref="CATALOG_SYNC"/> or <see cref="MIGRATE_LEGACY"/>.</summary>
        public string Command { get; private init; } = string.Empty;

        /// <summary>The connection string for <see cref="DatabasePath"/>.</summary>
        public string ConnectionString => $"Data Source={DatabasePath}";

        /// <summary>The SQLite file the command works on. Created with the full schema when it doesn't exist.</summary>
        public string DatabasePath { get; private init; } = string.Empty;

        /// <summary>For <see cref="MIGRATE_LEGACY"/>, the legacy database to read; otherwise null.</summary>
        public string? LegacyDatabasePath { get; private init; }

        /// <summary>When true, only the cache files already on disk are used; nothing is downloaded.</summary>
        public bool Offline { get; private init; }

        /// <summary>For <see cref="MIGRATE_LEGACY"/>, whether an existing database at <see cref="DatabasePath"/> may be replaced.</summary>
        public bool Overwrite { get; private init; }

        /// <summary>
        /// Returns the command the arguments ask for, or null when they don't start with a known command (so the web
        /// server starts as usual). Throws <see cref="ArgumentException"/> for a known command with bad options.
        /// </summary>
        public static CatalogCommandLine? Parse(IReadOnlyList<string> args)
        {
            if (args is null) throw new ArgumentNullException(nameof(args));
            if (args.Count == 0)
                return null;

            var command = args[0].ToLowerInvariant();
            if (command is not (CATALOG_SYNC or MIGRATE_LEGACY))
                return null;

            string? databasePath = null;
            string? legacyDatabasePath = null;
            var offline = false;
            var overwrite = false;
            for (var i = 1; i < args.Count; i++)
            {
                switch (args[i])
                {
                    case "--db" when command == CATALOG_SYNC && i + 1 < args.Count:
                    case "--to" when command == MIGRATE_LEGACY && i + 1 < args.Count:
                        databasePath = args[++i];
                        break;
                    case "--from" when command == MIGRATE_LEGACY && i + 1 < args.Count:
                        legacyDatabasePath = args[++i];
                        break;
                    case "--offline":
                        offline = true;
                        break;
                    case "--overwrite" when command == MIGRATE_LEGACY:
                        overwrite = true;
                        break;
                    default:
                        throw new ArgumentException($"Unknown option \"{args[i]}\". {Usage(command)}", nameof(args));
                }
            }

            if (string.IsNullOrWhiteSpace(databasePath) || (command == MIGRATE_LEGACY && string.IsNullOrWhiteSpace(legacyDatabasePath)))
                throw new ArgumentException($"Missing a required option. {Usage(command)}", nameof(args));

            return new CatalogCommandLine
            {
                Command = command,
                DatabasePath = databasePath,
                LegacyDatabasePath = legacyDatabasePath,
                Offline = offline,
                Overwrite = overwrite
            };
        }

        /// <summary>
        /// Readies the database file before the app opens it: <see cref="MIGRATE_LEGACY"/> needs a new file, so an existing
        /// one is deleted with <see cref="Overwrite"/> and refused without it. Does nothing for other commands.
        /// </summary>
        [ExcludeFromCodeCoverage(Justification = "File-system check and delete; no logic beyond the guard.")]
        public void PrepareDatabase()
        {
            if (Command != MIGRATE_LEGACY || !File.Exists(DatabasePath))
                return;

            if (!Overwrite)
                throw new InvalidOperationException($"{DatabasePath} already exists. Pass --overwrite to replace it.");

            LegacyMigrationRunner.DeleteDatabaseFiles(DatabasePath);
        }

        /// <summary>Runs the command and returns the process exit code: 0 on success.</summary>
        [ExcludeFromCodeCoverage(Justification = "Console orchestration over ICatalogSyncService and ILegacyMigrationService, which are tested directly.")]
        public async Task<int> RunAsync(IServiceProvider services)
        {
            if (services is null) throw new ArgumentNullException(nameof(services));

            var sourceMode = Offline ? CatalogSourceMode.CacheOnly : CatalogSourceMode.IfStale;
            if (Command == MIGRATE_LEGACY)
            {
                var logger = services.GetRequiredService<ILogger<CatalogCommandLine>>();
                var report = await LegacyMigrationRunner.RunAsync(services, LegacyDatabasePath!, DatabasePath, sourceMode, logger).ConfigureAwait(false);
                Console.WriteLine(report.ToText());
                return 0;
            }

            await using var scope = services.CreateAsyncScope();
            var syncService = scope.ServiceProvider.GetRequiredService<ICatalogSyncService>();
            var result = await syncService.SyncAsync(sourceMode).ConfigureAwait(false);
            var run = result.Run;
            if (result.Skipped)
            {
                Console.WriteLine($"Catalog sync skipped: the cached sources haven't changed since run {run.ID} finished.");
                return 0;
            }

            Console.WriteLine($"Catalog sync {run.ID}: {run.Status}{(run.IsBootstrap ? " (bootstrap)" : string.Empty)}");
            if (!string.IsNullOrWhiteSpace(run.Message))
                Console.WriteLine($"  {run.Message}");
            Console.WriteLine($"  Sources: yaml-yugi {run.YamlYugiStatus} ({run.YamlYugiCardCount}), YGOProDeck {run.YGOProDeckStatus} ({run.YGOProDeckCardCount}), TCGPlayer {run.TCGPlayerStatus} ({run.TCGPlayerProductCount} products)");
            Console.WriteLine($"  Cards added {run.CardsAdded}, updated {run.CardsUpdated}; printings added {run.PrintingsAdded}");
            Console.WriteLine($"  Products linked {run.ProductsLinked}, unmapped {run.ProductsUnmapped}, ignored {run.ProductsIgnored}");
            Console.WriteLine($"  Review items opened {run.ItemsOpened}, resolved by source {run.ItemsAutoResolved}");
            foreach (var (kind, count) in result.ItemsOpenedByKind.OrderBy(k => k.Key.ToString(), StringComparer.Ordinal))
                Console.WriteLine($"    {kind}: {count}");

            return run.Status == CatalogSyncRunStatus.Succeeded ? 0 : 1;
        }

        private static string Usage(string command) => command == MIGRATE_LEGACY
            ? $"Usage: {MIGRATE_LEGACY} --from <legacy path> --to <new path> [--offline] [--overwrite]"
            : $"Usage: {CATALOG_SYNC} --db <path> [--offline]";
    }
}
