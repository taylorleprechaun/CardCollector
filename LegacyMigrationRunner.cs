using CardCollector.Models.Catalog;
using CardCollector.Models.Legacy;
using CardCollector.Services;
using Microsoft.Data.Sqlite;
using System.Diagnostics.CodeAnalysis;

namespace CardCollector
{
    /// <summary>
    /// Runs the legacy migration for the command line and for startup: writes the report next to the new database, and on
    /// any failure deletes the half-built database so the next attempt starts clean. The legacy database is never touched.
    /// </summary>
    [ExcludeFromCodeCoverage(Justification = "File and DI-scope orchestration around ILegacyMigrationService, which is tested directly.")]
    public static class LegacyMigrationRunner
    {
        public const string REPORT_FILE_NAME = "migration-report.txt";

        /// <summary>Deletes a SQLite database file together with its WAL and shared-memory files.</summary>
        public static void DeleteDatabaseFiles(string databasePath)
        {
            SqliteConnection.ClearAllPools();
            foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
            {
                var path = databasePath + suffix;
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        public static async Task<LegacyMigrationReport> RunAsync(
            IServiceProvider services,
            string legacyDatabasePath,
            string targetDatabasePath,
            CatalogSourceMode sourceMode,
            ILogger logger)
        {
            if (services is null) throw new ArgumentNullException(nameof(services));
            if (logger is null) throw new ArgumentNullException(nameof(logger));

            try
            {
                LegacyMigrationReport report;
                await using (var scope = services.CreateAsyncScope())
                {
                    var migrationService = scope.ServiceProvider.GetRequiredService<ILegacyMigrationService>();
                    report = await migrationService.MigrateAsync(legacyDatabasePath, sourceMode).ConfigureAwait(false);
                }

                var reportPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(targetDatabasePath)) ?? ".", REPORT_FILE_NAME);
                await File.WriteAllTextAsync(reportPath, report.ToText()).ConfigureAwait(false);
                logger.LogInformation("Legacy migration report written to {ReportPath}", reportPath);
                return report;
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "Legacy migration from {LegacyDatabasePath} failed; removing the partly built {TargetDatabasePath}", legacyDatabasePath, targetDatabasePath);
                DeleteDatabaseFiles(targetDatabasePath);
                throw;
            }
        }
    }
}
