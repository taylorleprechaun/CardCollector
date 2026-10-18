using CardCollector.Data.Models;
using CardCollector.Data.Models.Catalog;
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

        [TestMethod]
        public async Task GetOpenItemsAsync_Search_ReturnsOnlyOpenMatchingItemsOfTheKinds()
        {
            using var context = InMemoryDbContextFactory.Create();
            context.CatalogReviewItems.AddRange(
                Item(1, CatalogReviewItemKind.CardAdded, "Token: Kuriboh added"),
                Item(2, CatalogReviewItemKind.CardAdded, "Synthetic Dragon added"),
                Item(3, CatalogReviewItemKind.CardAdded, "Token: Lamb added", CatalogReviewItemStatus.Dismissed),
                Item(4, CatalogReviewItemKind.PrintingAdded, "Token: Sheep printing added"));
            await context.SaveChangesAsync();

            var items = await new CatalogReviewRepository(context).GetOpenItemsAsync([CatalogReviewItemKind.CardAdded], "token");

            Assert.AreEqual(1, items.Single().ID);
        }

        [TestMethod]
        public async Task SearchAsync_NoQuery_ReturnsEveryItemOfTheStatusesAndKinds()
        {
            using var context = InMemoryDbContextFactory.Create();
            context.CatalogReviewItems.AddRange(
                Item(1, CatalogReviewItemKind.CardAdded, "First"),
                Item(2, CatalogReviewItemKind.CardAdded, "Second"));
            await context.SaveChangesAsync();

            var (_, totalCount) = await new CatalogReviewRepository(context)
                .SearchAsync([CatalogReviewItemStatus.Open], [CatalogReviewItemKind.CardAdded], "  ", 1, 25);

            Assert.AreEqual(2, totalCount);
        }

        [TestMethod]
        [DataRow("LOB-EN001", 1, DisplayName = "Summary")]
        [DataRow("retired", 2, DisplayName = "Resolution")]
        [DataRow("synthetic knight", 3, DisplayName = "Linked card name")]
        [DataRow("100%", 4, DisplayName = "Wildcard matched literally")]
        public async Task SearchAsync_Query_MatchesSummaryResolutionOrCardName(string query, int expectedID)
        {
            using var context = InMemoryDbContextFactory.Create();
            context.CatalogCards.Add(CatalogTestData.Card(30, "Synthetic Knight"));
            var resolved = Item(2, CatalogReviewItemKind.PrintingNotSeen, "Printing not seen", CatalogReviewItemStatus.Accepted);
            resolved.Resolution = "Printing SYN-EN002 Rare retired";
            var linked = Item(3, CatalogReviewItemKind.CardNotSeen, "Card not seen");
            linked.CatalogCardID = 30;
            context.CatalogReviewItems.AddRange(
                Item(1, CatalogReviewItemKind.PrintingNotSeen, "LOB-EN001 Ultra Rare not seen"),
                resolved,
                linked,
                Item(4, CatalogReviewItemKind.CardNotSeen, "Seen in 100% of runs"),
                Item(5, CatalogReviewItemKind.CardNotSeen, "Seen in 1000 runs"));
            await context.SaveChangesAsync();
            var statuses = Enum.GetValues<CatalogReviewItemStatus>();
            var kinds = Enum.GetValues<CatalogReviewItemKind>();

            var (items, totalCount) = await new CatalogReviewRepository(context).SearchAsync(statuses, kinds, query, 1, 25);

            Assert.AreEqual(expectedID, items.Single().ID);
            Assert.AreEqual(1, totalCount);
        }

        private static CatalogReviewItem Item(int id, CatalogReviewItemKind kind, string summary, CatalogReviewItemStatus status = CatalogReviewItemStatus.Open) => new()
        {
            ID = id,
            Kind = kind,
            Status = status,
            SubjectKey = $"subject:{id}",
            Summary = summary
        };
    }
}
