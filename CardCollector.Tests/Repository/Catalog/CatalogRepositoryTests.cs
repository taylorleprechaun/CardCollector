using CardCollector.Data.Models.Catalog;
using CardCollector.Repository.Catalog;
using CardCollector.Tests.TestHelpers;
using static CardCollector.Tests.TestHelpers.CatalogTestData;

namespace CardCollector.Tests.Repository.Catalog
{
    [TestClass]
    public sealed class CatalogRepositoryTests
    {
        [TestMethod]
        public async Task LoadSnapshotAsync_EmptyCatalog_ReturnsNoCards()
        {
            using var context = InMemoryDbContextFactory.Create();

            var state = await new CatalogRepository(context).LoadSnapshotAsync();

            Assert.AreEqual(0, state.Cards.Count);
        }

        [TestMethod]
        public async Task LoadSnapshotAsync_SavedCatalog_JoinsEveryCardToItsAliasesImagesAndPrintings()
        {
            using var context = InMemoryDbContextFactory.Create();
            var card = Card(1, "Synthetic Dragon", null, 11111111, (CatalogSource.YGOProDeck, CatalogAliasKind.YGOProDeckID, 22222222));
            card.Images.Add(new CatalogCardImage { CatalogCardID = 1, ImageID = 11111111, ImageURL = "https://example.test/1.jpg" });
            var ultra = Printing(card, 10, "SYN-EN001", "Ultra Rare", null, 500);
            Printing(card, 11, "SYN-EN001", "Secret Rare").Set = ultra.Set;
            var other = Card(2, "Synthetic Knight");
            Printing(other, 20, "OTH-EN001", "Common");
            context.CatalogCards.AddRange(card, other);
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();

            var state = await new CatalogRepository(context).LoadSnapshotAsync();

            var loaded = state.Cards.Single(c => c.ID == 1);
            Assert.AreEqual(22222222, loaded.Aliases.Single().Value);
            Assert.AreEqual(11111111, loaded.Images.Single().ImageID);
            CollectionAssert.AreEquivalent(new[] { 10, 11 }, loaded.Printings.Select(p => p.ID).ToArray());
            var linked = loaded.Printings.Single(p => p.ID == 10);
            Assert.AreSame(loaded, linked.Card);
            Assert.AreEqual("SYN", linked.Set!.Prefix);
            Assert.AreEqual("500", linked.Sources.Single().ExternalKey);
            Assert.AreEqual(0, loaded.Printings.Single(p => p.ID == 11).Sources.Count);
            Assert.AreEqual(20, state.Cards.Single(c => c.ID == 2).Printings.Single().ID);
            Assert.AreEqual(2, state.Sets.Count);
        }
    }
}
