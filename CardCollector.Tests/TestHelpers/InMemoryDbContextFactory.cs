using CardCollector.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CardCollector.Tests.TestHelpers
{
    // Fresh, isolated in-memory EF Core context per call — used to test Repository classes' query/grouping
    // logic directly without a real SQLite file or the ASP.NET pipeline.
    internal static class InMemoryDbContextFactory
    {
        public static AppDBContext Create()
        {
            var options = new DbContextOptionsBuilder<AppDBContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                // The InMemory provider doesn't support real transactions; without this it throws on
                // BeginTransactionAsync instead of treating it as a no-op the way UnitOfWork expects.
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            return new AppDBContext(options);
        }

        // The InMemory provider doesn't support ExecuteSqlRawAsync (used by ValueSnapshotRepository's
        // PruneSnapshotsAsync), so tests exercising that method need a real relational provider. SQLite's
        // in-memory mode gives one without touching disk; the connection must stay open for the context's
        // lifetime (and be disposed alongside it) since the database is destroyed when it closes.
        public static (AppDBContext Context, SqliteConnection Connection) CreateSqlite()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();

            var options = new DbContextOptionsBuilder<AppDBContext>()
                .UseSqlite(connection)
                .Options;

            var context = new AppDBContext(options);
            context.Database.EnsureCreated();

            return (context, connection);
        }
    }
}
