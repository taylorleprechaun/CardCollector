using CardCollector.Data.Models;
using CardCollector.Repository.Catalog;
using CardCollector.Tests.TestHelpers;

namespace CardCollector.Tests.Repository.Catalog
{
    [TestClass]
    public sealed class CatalogReviewRepositoryTests
    {
        [TestMethod]
        public async Task CountUserRowsForCardAsync_NoRows_ReturnsZero()
        {
            using var context = InMemoryDbContextFactory.Create();
            CatalogTestData.SeedPrintings(context, (101, 1));

            var count = await new CatalogReviewRepository(context).CountUserRowsForCardAsync(1);

            Assert.AreEqual(0, count);
        }

        [TestMethod]
        public async Task CountUserRowsForCardAsync_RowsOnSeveralPrintings_CountsEveryTableForThatCardOnly()
        {
            using var context = InMemoryDbContextFactory.Create();
            CatalogTestData.SeedPrintings(context, (101, 1), (102, 1), (201, 2));
            context.CollectionEntries.AddRange(
                new CollectionEntry { CatalogPrintingID = 101, Quantity = 3, Status = CollectionStatus.Owned },
                new CollectionEntry { CatalogPrintingID = 102, Quantity = 1, Status = CollectionStatus.Ordered },
                new CollectionEntry { CatalogPrintingID = 201, Quantity = 1, Status = CollectionStatus.Owned });
            context.PreferredVersions.Add(new PreferredVersion { CatalogPrintingID = 102 });
            context.CheckedOutCards.Add(new CheckedOutCard { CatalogPrintingID = 101, Quantity = 1 });
            context.PendingOrderLines.Add(new PendingOrderLine { CatalogPrintingID = 201, Quantity = 1 });
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();

            var count = await new CatalogReviewRepository(context).CountUserRowsForCardAsync(1);

            Assert.AreEqual(4, count);
        }
    }
}
