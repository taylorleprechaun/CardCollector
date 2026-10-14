using CardCollector.Data;
using CardCollector.Data.Models;
using CardCollector.Data.Models.Catalog;
using CardCollector.Tests.TestHelpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CardCollector.Tests.Data
{
    /// <summary>Saves user rows against a real in-memory SQLite database, where the save keeps both printing keys in step.</summary>
    [TestClass]
    public sealed class PrintingKeySyncTests
    {
        private SqliteConnection _connection = null!;
        private int _extendedArtID;
        private int _plainID;

        [TestMethod]
        public async Task SaveChanges_ColumnsChanged_ReResolvesPrintingID()
        {
            int entryID;
            using (var context = NewContext())
            {
                var entry = new CollectionEntry { CatalogPrintingID = _plainID, Quantity = 1, Status = CollectionStatus.Owned };
                context.CollectionEntries.Add(entry);
                await context.SaveChangesAsync();
                entryID = entry.ID;
            }

            using (var context = NewContext())
            {
                var entry = await context.CollectionEntries.SingleAsync(e => e.ID == entryID);
                entry.PrintVariant = "Extended Art";
                await context.SaveChangesAsync();
            }

            using var verify = NewContext();
            Assert.AreEqual(_extendedArtID, (await verify.CollectionEntries.SingleAsync()).CatalogPrintingID);
        }

        [TestMethod]
        public async Task SaveChanges_ColumnsMatchNoPrinting_LeavesPrintingIDEmpty()
        {
            using (var context = NewContext())
            {
                context.CollectionEntries.Add(new CollectionEntry { CardID = CardID(), RarityName = "Secret Rare", SetCode = "RA05-EN146", Status = CollectionStatus.Owned });
                await context.SaveChangesAsync();
            }

            using var verify = NewContext();
            Assert.IsNull((await verify.CollectionEntries.SingleAsync()).CatalogPrintingID);
        }

        [TestMethod]
        public async Task SaveChanges_OnlyColumnsGiven_FillsPrintingID()
        {
            using (var context = NewContext())
            {
                context.PreferredVersions.Add(new PreferredVersion { CardID = CardID(), PrintVariant = "Extended Art", RarityName = "Ultra Rare", SetCode = "RA05-EN146" });
                await context.SaveChangesAsync();
            }

            using var verify = NewContext();
            Assert.AreEqual(_extendedArtID, (await verify.PreferredVersions.SingleAsync()).CatalogPrintingID);
        }

        [TestMethod]
        public async Task SaveChanges_OnlyPrintingIDGiven_FillsColumnsFromPrinting()
        {
            using (var context = NewContext())
            {
                context.CheckedOutCards.Add(new CheckedOutCard { CatalogPrintingID = _extendedArtID, Quantity = 2 });
                await context.SaveChangesAsync();
            }

            using var verify = NewContext();
            var saved = await verify.CheckedOutCards.SingleAsync();
            Assert.AreEqual(CardID(), saved.CardID);
            Assert.AreEqual("RA05-EN146", saved.SetCode);
            Assert.AreEqual("Ultra Rare", saved.RarityName);
            Assert.AreEqual("Extended Art", saved.PrintVariant);
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
            var plain = CatalogTestData.Printing(card, 0, "RA05-EN146", "Ultra Rare");
            var extendedArt = CatalogTestData.Printing(card, 0, "RA05-EN146", "Ultra Rare", "Extended Art");
            extendedArt.Set = plain.Set;
            plain.CatalogCardID = extendedArt.CatalogCardID = 0;
            context.CatalogCards.Add(card);
            context.SaveChanges();
            _plainID = plain.ID;
            _extendedArtID = extendedArt.ID;
        }

        private int CardID()
        {
            using var context = NewContext();
            return context.CatalogCards.Single().ID;
        }

        private AppDBContext NewContext() =>
            new(new DbContextOptionsBuilder<AppDBContext>().UseSqlite(_connection).Options);
    }
}
