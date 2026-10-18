using CardCollector.Models.Legacy;

namespace CardCollector.Repository.Legacy
{
    /// <summary>Reads the legacy (pre-catalog) database file, opened read-only so the migration can never change it.</summary>
    public interface ILegacyDatabaseReader
    {
        /// <summary>Returns the names of the tables in the legacy database.</summary>
        Task<IReadOnlySet<string>> GetTableNamesAsync(string databasePath, CancellationToken cancellationToken = default);

        /// <summary>Returns every row of a table. The table must exist (see <see cref="GetTableNamesAsync"/>).</summary>
        Task<IReadOnlyList<LegacyRow>> ReadTableAsync(string databasePath, string table, CancellationToken cancellationToken = default);
    }
}
