using CardCollector.Models.Legacy;
using Microsoft.Data.Sqlite;

namespace CardCollector.Repository.Legacy
{
    public sealed class LegacyDatabaseReader : ILegacyDatabaseReader
    {
        public async Task<IReadOnlySet<string>> GetTableNamesAsync(string databasePath, CancellationToken cancellationToken = default)
        {
            await using var connection = await OpenAsync(databasePath, cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table'";

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                names.Add(reader.GetString(0));

            return names;
        }

        public async Task<IReadOnlyList<LegacyRow>> ReadTableAsync(string databasePath, string table, CancellationToken cancellationToken = default)
        {
            var tables = await GetTableNamesAsync(databasePath, cancellationToken).ConfigureAwait(false);
            if (!tables.Contains(table))
                throw new ArgumentException($"The legacy database has no table \"{table}\".", nameof(table));

            await using var connection = await OpenAsync(databasePath, cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            // The name was checked against sqlite_master above, so quoting it cannot inject anything.
            command.CommandText = $"SELECT * FROM \"{table}\" ORDER BY ID";

            var rows = new List<LegacyRow>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < reader.FieldCount; i++)
                    values[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                rows.Add(new LegacyRow(values));
            }

            return rows;
        }

        private static async Task<SqliteConnection> OpenAsync(string databasePath, CancellationToken cancellationToken)
        {
            if (!File.Exists(databasePath))
                throw new FileNotFoundException("The legacy database was not found.", databasePath);

            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            }.ToString();
            var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
    }
}
