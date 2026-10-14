using CardCollector.Data;
using CardCollector.Tests.TestHelpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CardCollector.Tests.Data
{
    /// <summary>Runs the start-up backfill against a real in-memory SQLite database.</summary>
    [TestClass]
    public sealed class PrintingKeyBackfillTests
    {
        private int _cardID;
        private SqliteConnection _connection = null!;
        private int _printingID;

        [TestMethod]
        public void Apply_RowsWithoutPrintingID_FillsOnlyTheOnesThatMatchAPrinting()
        {
            using (var context = NewContext())
            {
                // Raw inserts skip the save-time sync, like rows written before it existed.
                InsertTrackedPrinting(context, "Ultra Rare");
                InsertTrackedPrinting(context, "Secret Rare");
            }

            int filled;
            using (var context = NewContext())
                filled = PrintingKeyBackfill.Apply(context);

            Assert.AreEqual(1, filled);
            using var verify = NewContext();
            CollectionAssert.AreEquivalent(new int?[] { _printingID, null }, verify.PreferredVersions.Select(p => p.CatalogPrintingID).ToArray());
        }

        [TestMethod]
        public void Apply_RunTwice_FillsNothingTheSecondTime()
        {
            using (var context = NewContext())
                InsertTrackedPrinting(context, "Ultra Rare");
            using (var context = NewContext())
                PrintingKeyBackfill.Apply(context);

            int filledAgain;
            using (var context = NewContext())
                filledAgain = PrintingKeyBackfill.Apply(context);

            Assert.AreEqual(0, filledAgain);
        }

        [TestCleanup]
        public void Cleanup() => _connection.Dispose();

        [TestInitialize]
        public void Initialize()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            using var context = NewContext();
            context.Database.EnsureCreated();
            var card = CatalogTestData.Card(0, "Synthetic Dragon");
            var printing = CatalogTestData.Printing(card, 0, "RA05-EN146", "Ultra Rare");
            printing.CatalogCardID = 0;
            context.CatalogCards.Add(card);
            context.SaveChanges();
            _cardID = card.ID;
            _printingID = printing.ID;
        }

        private void InsertTrackedPrinting(AppDBContext context, string rarityName) =>
            context.Database.ExecuteSqlRaw(
                """INSERT INTO "PreferredVersions" ("CardID", "SetCode", "RarityName", "PrintVariant", "DesiredQuantity", "DateCreated", "DateModified") VALUES ({0}, 'RA05-EN146', {1}, NULL, 3, '2026-10-14', '2026-10-14')""",
                _cardID, rarityName);

        private AppDBContext NewContext() =>
            new(new DbContextOptionsBuilder<AppDBContext>().UseSqlite(_connection).Options);
    }
}
