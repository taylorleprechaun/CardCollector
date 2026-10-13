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
        [DataRow(new string[0], DisplayName = "No arguments")]
        [DataRow(new[] { "--urls", "http://localhost:5000" }, DisplayName = "Web server arguments")]
        public void Parse_NotACommand_ReturnsNull(string[] args)
        {
            var command = CatalogCommandLine.Parse(args);

            Assert.IsNull(command);
        }
    }
}
