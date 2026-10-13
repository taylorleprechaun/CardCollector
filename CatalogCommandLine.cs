using System.Diagnostics.CodeAnalysis;
using CardCollector.Data.Models.Catalog;
using CardCollector.Models.Catalog;
using CardCollector.Services;

namespace CardCollector
{
    /// <summary>
    /// The maintenance commands the app runs instead of starting the web server, e.g.
    /// <c>dotnet run -- catalog-sync --db Data/cardcollector.db --offline</c>.
    /// </summary>
    public sealed class CatalogCommandLine
    {
        public const string CATALOG_SYNC = "catalog-sync";

        private CatalogCommandLine()
        {
        }

        /// <summary>The command name, e.g. <see cref="CATALOG_SYNC"/>.</summary>
        public string Command { get; private init; } = string.Empty;

        /// <summary>The connection string for <see cref="DatabasePath"/>.</summary>
        public string ConnectionString => $"Data Source={DatabasePath}";

        /// <summary>The SQLite file the command works on. Created with the full schema when it doesn't exist.</summary>
        public string DatabasePath { get; private init; } = string.Empty;

        /// <summary>When true, only the cache files already on disk are used; nothing is downloaded.</summary>
        public bool Offline { get; private init; }

        /// <summary>
        /// Returns the command the arguments ask for, or null when they don't start with a known command (so the web
        /// server starts as usual). Throws <see cref="ArgumentException"/> for a known command with bad options.
        /// </summary>
        public static CatalogCommandLine? Parse(IReadOnlyList<string> args)
        {
            if (args is null) throw new ArgumentNullException(nameof(args));
            if (args.Count == 0 || !string.Equals(args[0], CATALOG_SYNC, StringComparison.OrdinalIgnoreCase))
                return null;

            string? databasePath = null;
            var offline = false;
            for (var i = 1; i < args.Count; i++)
            {
                switch (args[i])
                {
                    case "--db" when i + 1 < args.Count:
                        databasePath = args[++i];
                        break;
                    case "--offline":
                        offline = true;
                        break;
                    default:
                        throw new ArgumentException($"Unknown option \"{args[i]}\". Usage: {CATALOG_SYNC} --db <path> [--offline]", nameof(args));
                }
            }

            if (string.IsNullOrWhiteSpace(databasePath))
                throw new ArgumentException($"{CATALOG_SYNC} needs --db <path>.", nameof(args));

            return new CatalogCommandLine { Command = CATALOG_SYNC, DatabasePath = databasePath, Offline = offline };
        }

        /// <summary>Runs the command and returns the process exit code: 0 when the sync succeeded.</summary>
        [ExcludeFromCodeCoverage(Justification = "Console orchestration over ICatalogSyncService, which is tested directly.")]
        public async Task<int> RunAsync(IServiceProvider services)
        {
            if (services is null) throw new ArgumentNullException(nameof(services));

            await using var scope = services.CreateAsyncScope();
            var syncService = scope.ServiceProvider.GetRequiredService<ICatalogSyncService>();
            var result = await syncService.SyncAsync(Offline ? CatalogSourceMode.CacheOnly : CatalogSourceMode.IfStale).ConfigureAwait(false);
            var run = result.Run;

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
    }
}
