using CardCollector.Models.Catalog;
using CardCollector.Models.Legacy;

namespace CardCollector.Services
{
    /// <summary>
    /// Builds the app's database from a legacy (pre-catalog) one: creates the catalog from the sources, then carries every
    /// user row over, pointed at catalog cards and printings. The legacy database is only read.
    /// </summary>
    public interface ILegacyMigrationService
    {
        /// <summary>
        /// Migrates into the app's configured database, which should be new and empty. Rows that can't be mapped become
        /// review items instead of being dropped. Throws when the catalog can't be built.
        /// </summary>
        Task<LegacyMigrationReport> MigrateAsync(string legacyDatabasePath, CatalogSourceMode sourceMode, CancellationToken cancellationToken = default);
    }
}
