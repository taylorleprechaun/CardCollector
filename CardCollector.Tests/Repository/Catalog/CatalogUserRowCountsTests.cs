using CardCollector.Data.Models;
using CardCollector.Repository.Catalog;
using CardCollector.Tests.TestHelpers;

namespace CardCollector.Tests.Repository.Catalog
{
    [TestClass]
    public sealed class CatalogUserRowCountsTests
    {
        [TestMethod]
        public async Task CountUserRowsByPrintingAsync_CardFilter_CountsEachPrintingsRowsAcrossTables()
        {
            using var context = InMemoryDbContextFactory.Create();
            CatalogTestData.SeedPrintings(context, (101, 1), (102, 1), (103, 1), (201, 2));
            context.CollectionEntries.AddRange(
                new CollectionEntry { CatalogPrintingID = 101, Quantity = 1, Status = CollectionStatus.Owned },
                new CollectionEntry { CatalogPrintingID = 101, Quantity = 2, Status = CollectionStatus.Ordered },
                new CollectionEntry { CatalogPrintingID = 201, Quantity = 1, Status = CollectionStatus.Owned });
            context.PreferredVersions.Add(new PreferredVersion { CatalogPrintingID = 102 });
            context.CheckedOutCards.Add(new CheckedOutCard { CatalogPrintingID = 101, Quantity = 1 });
            context.PendingOrderLines.Add(new PendingOrderLine { CatalogPrintingID = 102, Quantity = 1 });
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();

            var counts = await context.CountUserRowsByPrintingAsync(p => p.CatalogCardID == 1);

            Assert.AreEqual(2, counts.Count);
            Assert.AreEqual(3, counts[101]);
            Assert.AreEqual(2, counts[102]);
        }

        [TestMethod]
        public async Task CountUserRowsByPrintingAsync_PrintingFilter_CountsOnlyThatPrinting()
        {
            using var context = InMemoryDbContextFactory.Create();
            CatalogTestData.SeedPrintings(context, (101, 1), (102, 1));
            context.CollectionEntries.AddRange(
                new CollectionEntry { CatalogPrintingID = 101, Quantity = 1, Status = CollectionStatus.Owned },
                new CollectionEntry { CatalogPrintingID = 102, Quantity = 1, Status = CollectionStatus.Owned });
            await context.SaveChangesAsync();

            var counts = await context.CountUserRowsByPrintingAsync(p => p.ID == 102);

            Assert.AreEqual(1, counts.Single().Value);
            Assert.AreEqual(102, counts.Single().Key);
        }
    }
}
