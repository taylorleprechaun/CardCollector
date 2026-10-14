using CardCollector.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace CardCollector.Repository.Legacy
{
    public sealed class LegacyImportRepository : ILegacyImportRepository
    {
        private const string LEGACY_SCHEMA = "legacy";

        private readonly AppDBContext _context;

        public LegacyImportRepository(AppDBContext context)
        {
            _context = context;
        }

        public void AddRange<TEntity>(IEnumerable<TEntity> entities) where TEntity : class =>
            _context.Set<TEntity>().AddRange(entities);

        public async Task<int> CopyTableAsync(string legacyDatabasePath, string table, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(legacyDatabasePath)) throw new ArgumentException("A legacy database path is required.", nameof(legacyDatabasePath));
            if (string.IsNullOrWhiteSpace(table)) throw new ArgumentException("A table name is required.", nameof(table));

            // ATTACH only lasts for the connection, so keep one open across the statements below.
            await _context.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await _context.Database.ExecuteSqlRawAsync($"ATTACH DATABASE {{0}} AS {LEGACY_SCHEMA}", [legacyDatabasePath], cancellationToken).ConfigureAwait(false);
                try
                {
                    var targetColumns = await GetColumnsAsync(table, "main", cancellationToken).ConfigureAwait(false);
                    var legacyColumns = await GetColumnsAsync(table, LEGACY_SCHEMA, cancellationToken).ConfigureAwait(false);
                    var columns = targetColumns.Where(legacyColumns.Contains).ToList();
                    if (columns.Count == 0)
                        throw new InvalidOperationException($"Table \"{table}\" has no columns in common between the two databases.");

                    // Table and column names come from sqlite_master/pragma_table_info, never from input, so quoting is enough.
                    var columnList = string.Join(", ", columns.Select(c => $"\"{c}\""));
#pragma warning disable EF1002
                    return await _context.Database.ExecuteSqlRawAsync(
                        $"INSERT INTO main.\"{table}\" ({columnList}) SELECT {columnList} FROM {LEGACY_SCHEMA}.\"{table}\"",
                        cancellationToken).ConfigureAwait(false);
#pragma warning restore EF1002
                }
                finally
                {
                    await _context.Database.ExecuteSqlRawAsync($"DETACH DATABASE {LEGACY_SCHEMA}", CancellationToken.None).ConfigureAwait(false);
                }
            }
            finally
            {
                await _context.Database.CloseConnectionAsync().ConfigureAwait(false);
            }
        }

        public async Task EnsureCreatedAsync(CancellationToken cancellationToken = default) =>
            await _context.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);

        public async Task<TEntity?> FindAsync<TEntity>(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
            where TEntity : class =>
            await _context.Set<TEntity>().FirstOrDefaultAsync(predicate, cancellationToken).ConfigureAwait(false);

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        private async Task<IReadOnlyList<string>> GetColumnsAsync(string table, string schema, CancellationToken cancellationToken) =>
            await _context.Database
                .SqlQuery<string>($"SELECT name FROM pragma_table_info({table}, {schema})")
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
    }
}
