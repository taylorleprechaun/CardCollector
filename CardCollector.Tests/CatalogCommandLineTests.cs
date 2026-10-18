namespace CardCollector.Tests
{
    [TestClass]
    public sealed class CatalogCommandLineTests
    {
        [TestMethod]
        public void Parse_CatalogSyncWithDatabaseAndOffline_ReturnsCommand()
        {
            var command = CatalogCommandLine.Parse(["catalog-sync", "--db", "Data/catalog.db", "--offline"]);

            Assert.IsNotNull(command);
            Assert.AreEqual("Data/catalog.db", command.DatabasePath);
            Assert.IsTrue(command.Offline);
            Assert.AreEqual("Data Source=Data/catalog.db", command.ConnectionString);
        }

        [TestMethod]
        public void Parse_CatalogSyncWithoutDatabase_Throws()
        {
            Assert.ThrowsExactly<ArgumentException>(() => CatalogCommandLine.Parse(["catalog-sync", "--offline"]));
        }

        [TestMethod]
        public void Parse_CatalogSyncWithUnknownOption_Throws()
        {
            Assert.ThrowsExactly<ArgumentException>(() => CatalogCommandLine.Parse(["catalog-sync", "--db", "x.db", "--verbose"]));
        }

        [TestMethod]
        public void Parse_MigrateLegacyWithoutFrom_Throws()
        {
            Assert.ThrowsExactly<ArgumentException>(() => CatalogCommandLine.Parse(["migrate-legacy", "--to", "Data/new.db"]));
        }

        [TestMethod]
        public void Parse_MigrateLegacyWithPaths_ReturnsCommand()
        {
            var command = CatalogCommandLine.Parse(["migrate-legacy", "--from", "Data/collection.db", "--to", "Data/new.db", "--offline", "--overwrite"]);

            Assert.IsNotNull(command);
            Assert.AreEqual(CatalogCommandLine.MIGRATE_LEGACY, command.Command);
            Assert.AreEqual("Data/collection.db", command.LegacyDatabasePath);
            Assert.AreEqual("Data/new.db", command.DatabasePath);
            Assert.IsTrue(command.Offline);
            Assert.IsTrue(command.Overwrite);
        }

        [TestMethod]
        [DataRow(new string[0], DisplayName = "No arguments")]
        [DataRow(new[] { "--urls", "http://localhost:5000" }, DisplayName = "Web server arguments")]
        public void Parse_NotACommand_ReturnsNull(string[] args)
        {
            var command = CatalogCommandLine.Parse(args);

            Assert.IsNull(command);
        }

        [TestMethod]
        public void Parse_OverwriteOnCatalogSync_Throws()
        {
            Assert.ThrowsExactly<ArgumentException>(() => CatalogCommandLine.Parse(["catalog-sync", "--db", "x.db", "--overwrite"]));
        }
    }
}
