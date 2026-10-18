using CardCollector.Data.Models.Catalog;
using CardCollector.Models.Catalog;
using CardCollector.Services;
using System.Diagnostics.CodeAnalysis;

namespace CardCollector
{
    /// <summary>
    /// The maintenance command the app runs instead of starting the web server: <c>catalog-sync --db &lt;path&gt; [--offline]</c>
    /// builds or updates a catalog database.
    /// </summary>
    public sealed class CatalogCommandLine
    {
        public const string CATALOG_SYNC = "catalog-sync";

        private const string USAGE = $"Usage: {CATALOG_SYNC} --db <path> [--offline]";

        private CatalogCommandLine()
        {
        }

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
                        throw new ArgumentException($"Unknown option \"{args[i]}\". {USAGE}", nameof(args));
                }
            }

            if (string.IsNullOrWhiteSpace(databasePath))
                throw new ArgumentException($"Missing a required option. {USAGE}", nameof(args));

            return new CatalogCommandLine
            {
                DatabasePath = databasePath,
                Offline = offline
            };
        }

        /// <summary>Runs the command and returns the process exit code: 0 on success.</summary>
        [ExcludeFromCodeCoverage(Justification = "Console orchestration over ICatalogSyncService, which is tested directly.")]
        public async Task<int> RunAsync(IServiceProvider services)
        {
            if (services is null) throw new ArgumentNullException(nameof(services));

            var sourceMode = Offline ? CatalogSourceMode.CacheOnly : CatalogSourceMode.IfStale;
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
    }
}
