using CardCollector.Data.Models.Catalog;
using CardCollector.Rules.Catalog;
using static CardCollector.Tests.TestHelpers.CatalogTestData;

namespace CardCollector.Tests.Rules.Catalog
{
    [TestClass]
    public sealed class CatalogPrintingFactoryTests
    {
        [TestMethod]
        public async Task CreateByHandAsync_NoSiblingAndNoSetWithThePrefix_AddsANewSet()
        {
            var card = Card(1, "Synthetic Dragon");
            CatalogSet? added = null;

            var printing = await CatalogPrintingFactory.CreateByHandAsync(
                card, "NEW-EN001", "Rare", null, "Added by hand", _ => Task.FromResult<CatalogSet?>(null), set => added = set, NowUTC);

            Assert.IsNotNull(added);
            Assert.AreEqual("NEW", added.Prefix);
            Assert.AreSame(added, printing.Set);
            Assert.AreEqual("NEW", printing.SetName);
        }

        [TestMethod]
        public async Task CreateByHandAsync_SetWithThePrefixExists_JoinsIt()
        {
            var card = Card(1, "Synthetic Dragon");
            var existing = new CatalogSet { Name = "Synthetic Collection", Prefix = "SYN" };

            var printing = await CatalogPrintingFactory.CreateByHandAsync(
                card, "SYN-EN009", "Rare", null, "Added by hand", prefix => Task.FromResult<CatalogSet?>(prefix == "SYN" ? existing : null), _ => Assert.Fail("No set should be added."), NowUTC);

            Assert.AreSame(existing, printing.Set);
            Assert.AreEqual("Synthetic Collection", printing.SetName);
        }

        [TestMethod]
        public async Task CreateByHandAsync_SiblingUnderTheSetCode_UsesItsSetAndAddsAManualSource()
        {
            var card = Card(1, "Synthetic Dragon");
            var sibling = Printing(card, 10, "SYN-EN001", "Ultra Rare");

            var printing = await CatalogPrintingFactory.CreateByHandAsync(
                card, "SYN-EN001", "Secret Rare", "Alternate Art", "Created from the review inbox",
                _ => throw new InvalidOperationException("The sibling's set should be used."), _ => Assert.Fail("No set should be added."), NowUTC);

            Assert.AreSame(sibling.Set, printing.Set);
            Assert.AreEqual(sibling.SetName, printing.SetName);
            Assert.AreEqual(2, card.Printings.Count);
            var source = printing.Sources.Single();
            Assert.AreEqual(CatalogSource.Manual, source.Source);
            Assert.AreEqual("Created from the review inbox", source.ExternalSummary);
            StringAssert.StartsWith(source.ExternalKey, "manual:");
        }

        [TestMethod]
        [DataRow("Ultra Rare", null, "Ultra Rare", null, DisplayName = "No variant")]
        [DataRow("Ultra Rare", "  ", "Ultra Rare", null, DisplayName = "Blank variant")]
        [DataRow("Ultra Rare", "Ultra Rare", "Ultra Rare", null, DisplayName = "Variant restates the rarity")]
        [DataRow("Ultra Rare", "Alternate Art", "Ultra Rare", "Alternate Art", DisplayName = "Real variant")]
        public void NormalizeRarityAndVariant_ReportedValues_DropsVariantsThatAddNothing(string rawRarity, string? rawVariant, string expectedRarity, string? expectedVariant)
        {
            var (rarity, variant) = CatalogPrintingFactory.NormalizeRarityAndVariant(rawRarity, rawVariant);

            Assert.AreEqual(expectedRarity, rarity);
            Assert.AreEqual(expectedVariant, variant);
        }
    }
}
