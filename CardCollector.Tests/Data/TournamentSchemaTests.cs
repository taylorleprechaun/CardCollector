using CardCollector.Data;
using CardCollector.Tests.TestHelpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CardCollector.Tests.Data
{
    [TestClass]
    public sealed class TournamentSchemaTests
    {
        private static readonly string[] TournamentTables = ["Formats", "FormatStrategies"];

        [TestMethod]
        public void Apply_CalledTwice_DoesNotThrowAndKeepsSchema()
        {
            using var patched = CreatePatchedDatabase();
            var before = DescribeSchema(patched.Connection);

            TournamentSchema.Apply(patched.Context);

            CollectionAssert.AreEqual(before, DescribeSchema(patched.Connection));
        }

        [TestMethod]
        public void Apply_ExistingDatabaseWithoutTables_MatchesEnsureCreatedSchema()
        {
            var (ensureCreatedContext, ensureCreatedConnection) = InMemoryDbContextFactory.CreateSqlite();
            using var _ = ensureCreatedContext;
            using var __ = ensureCreatedConnection;
            using var patched = CreatePatchedDatabase();

            var expected = DescribeSchema(ensureCreatedConnection);
            var actual = DescribeSchema(patched.Connection);

            Assert.IsTrue(expected.Length > 0, "EnsureCreated schema was empty.");
            CollectionAssert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void Apply_ExistingRows_AreUntouched()
        {
            using var patched = CreatePatchedDatabase();
            patched.Context.Database.ExecuteSqlRaw(
                "INSERT INTO \"Formats\" (\"DateCreated\", \"DateModified\", \"Name\", \"StartDate\") VALUES ('2024-01-01 00:00:00', '2024-01-01 00:00:00', 'Alpha Era', '2024-01-01')");

            TournamentSchema.Apply(patched.Context);

            Assert.AreEqual(1, patched.Context.Formats.Count());
        }

        [TestMethod]
        public void Apply_NullContext_ThrowsArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => TournamentSchema.Apply(null!));
        }

        private static PatchedDatabase CreatePatchedDatabase()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            var context = new AppDBContext(new DbContextOptionsBuilder<AppDBContext>().UseSqlite(connection).Options);
            TournamentSchema.Apply(context);
            return new PatchedDatabase(context, connection);
        }

        // Column name/type/nullability/PK, indexes (name + uniqueness + columns) and foreign keys for each Tournaments table.
        private static string[] DescribeSchema(SqliteConnection connection)
        {
            var lines = new List<string>();
            foreach (var table in TournamentTables)
            {
                lines.AddRange(Query(connection,
                    $"SELECT 'col ' || name || ' ' || type || ' notnull=' || \"notnull\" || ' pk=' || pk || ' default=' || IFNULL(dflt_value, 'none') FROM pragma_table_info('{table}') ORDER BY name",
                    table));

                lines.AddRange(Query(connection,
                    $"SELECT 'idx ' || il.name || ' unique=' || il.\"unique\" || ' cols=' || (SELECT group_concat(name) FROM pragma_index_info(il.name)) FROM pragma_index_list('{table}') il WHERE il.origin = 'c' ORDER BY il.name",
                    table));

                lines.AddRange(Query(connection,
                    $"SELECT 'fk ' || \"from\" || '->' || \"table\" || '.' || \"to\" || ' delete=' || on_delete FROM pragma_foreign_key_list('{table}') ORDER BY \"from\"",
                    table));
            }

            return [.. lines];
        }

        private static IEnumerable<string> Query(SqliteConnection connection, string sql, string table)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            using var reader = command.ExecuteReader();
            while (reader.Read())
                yield return $"{table}: {reader.GetString(0)}";
        }

        private sealed class PatchedDatabase : IDisposable
        {
            public PatchedDatabase(AppDBContext context, SqliteConnection connection)
            {
                Connection = connection;
                Context = context;
            }

            public SqliteConnection Connection { get; }

            public AppDBContext Context { get; }

            public void Dispose()
            {
                Context.Dispose();
                Connection.Dispose();
            }
        }
    }
}
