using Microsoft.Extensions.Logging.Abstractions;

namespace CardCollector.Tests
{
    [TestClass]
    public sealed class LegacyMigrationRunnerTests
    {
        private string _directory = null!;

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, recursive: true);
        }

        [TestMethod]
        public void DiscardIncompleteMigration_MarkerLeftBehind_DeletesDatabaseFilesAndMarker()
        {
            var databasePath = Path.Combine(_directory, "synthetic.db");
            File.WriteAllText(databasePath, "partial");
            File.WriteAllText(databasePath + "-wal", "partial");
            File.WriteAllText(databasePath + LegacyMigrationRunner.IN_PROGRESS_SUFFIX, "started");

            var discarded = LegacyMigrationRunner.DiscardIncompleteMigration(databasePath, NullLogger.Instance);

            Assert.IsTrue(discarded);
            Assert.IsFalse(File.Exists(databasePath));
            Assert.IsFalse(File.Exists(databasePath + "-wal"));
            Assert.IsFalse(File.Exists(databasePath + LegacyMigrationRunner.IN_PROGRESS_SUFFIX));
        }

        [TestMethod]
        public void DiscardIncompleteMigration_NoMarker_LeavesTheDatabaseAlone()
        {
            var databasePath = Path.Combine(_directory, "synthetic.db");
            File.WriteAllText(databasePath, "complete");

            var discarded = LegacyMigrationRunner.DiscardIncompleteMigration(databasePath, NullLogger.Instance);

            Assert.IsFalse(discarded);
            Assert.IsTrue(File.Exists(databasePath));
        }

        [TestInitialize]
        public void Initialize()
        {
            _directory = Path.Combine(Path.GetTempPath(), "cardcollector-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
        }
    }
}
