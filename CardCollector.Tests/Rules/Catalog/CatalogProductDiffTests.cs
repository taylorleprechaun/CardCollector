using CardCollector.Data.Models.Catalog;
using CardCollector.Models.Catalog;
using CardCollector.Rules.Catalog;
using static CardCollector.Tests.TestHelpers.CatalogTestData;

namespace CardCollector.Tests.Rules.Catalog
{
    [TestClass]
    public sealed class CatalogProductDiffTests
    {
        [TestMethod]
        public void Apply_AlreadyLinkedProduct_KeepsItsPrintingEvenIfVariantTextChanges()
        {
            var card = Card(1, "Synthetic Dragon");
            var printing = Printing(card, 10, "RA05-EN146", "Ultra Rare", "Extended Art", 687324);
            printing.Sources.Single().ExternalSummary = Product(687324, "Synthetic Dragon", "RA05-EN146", "Ultra Rare", "Extended Art").Summary;

            var result = Run(State(card), Context(), Product(687324, "Synthetic Dragon", "RA05-EN146", "Ultra Rare"));

            Assert.AreEqual("Extended Art", printing.PrintVariant);
            Assert.AreEqual(0, result.ProductsLinked);
            Assert.AreEqual(1, card.Printings.Count);
            Assert.AreEqual(CatalogReviewItemKind.TCGProductChanged, result.Items.Single().Kind);
        }

        [TestMethod]
        public void Apply_ExactPrintingExists_LinksProductToIt()
        {
            var card = Card(1, "Synthetic Dragon");
            var printing = Printing(card, 10, "SYN-EN001", "Ultra Rare");

            var result = Run(State(card), Context(), Product(500, "Synthetic Dragon", "SYN-EN001", "Ultra Rare"));

            Assert.AreEqual("500", printing.Sources.Single().ExternalKey);
            Assert.AreEqual(1, result.ProductsLinked);
            Assert.AreEqual(0, result.Items.Count);
        }

        [TestMethod]
        public void Apply_LinkedProductDisappears_ReportsListingChange()
        {
            var card = Card(1, "Synthetic Dragon");
            var plain = Printing(card, 10, "SYN-EN001", "Ultra Rare", null, 500);
            var variant = Printing(card, 11, "SYN-EN001", "Ultra Rare", "Extended Art", 501);
            var stillListed = Product(501, "Synthetic Dragon", "SYN-EN001", "Ultra Rare", "Extended Art");
            variant.Sources.Single().ExternalSummary = stillListed.Summary;

            var result = Run(State(card), Context(), stillListed);

            var item = result.Items.Single();
            Assert.AreEqual(CatalogReviewItemKind.TCGListingChanged, item.Kind);
            Assert.AreSame(plain, item.Printing);
        }

        [TestMethod]
        public void Apply_NewRarityUnderKnownSetCode_CreatesPrinting()
        {
            var card = Card(1, "Synthetic Dragon");
            Printing(card, 10, "SYN-EN001", "Ultra Rare");

            var result = Run(State(card), Context(), Product(500, "Synthetic Dragon", "SYN-EN001", "Starlight Rare"));

            var created = card.Printings.Single(p => p.RarityName == "Starlight Rare");
            Assert.AreEqual("500", created.Sources.Single().ExternalKey);
            Assert.AreEqual(1, result.PrintingsAdded);
            Assert.AreEqual(CatalogReviewItemKind.PrintingAdded, result.Items.Single().Kind);
        }

        [TestMethod]
        public void Apply_NewVariantProduct_CreatesVariantPrinting()
        {
            var card = Card(1, "Synthetic Dragon");
            Printing(card, 10, "SYN-EN001", "Ultra Rare");

            Run(State(card), Context(), Product(500, "Synthetic Dragon", "SYN-EN001", "Ultra Rare", "Extended Art"));

            Assert.AreEqual(2, card.Printings.Count);
            Assert.AreEqual("Extended Art", card.Printings.Last().PrintVariant);
        }

        [TestMethod]
        public void Apply_SetCodeSharedByTwoCards_UsesCardName()
        {
            var first = Card(1, "Synthetic Dragon");
            var second = Card(2, "Synthetic Wyvern");
            Printing(first, 10, "OLD-001", "Rare");
            var wanted = Printing(second, 20, "OLD-001", "Rare");

            Run(State(first, second), Context(), Product(500, "Synthetic Wyvern", "OLD-001", "Rare"));

            Assert.AreEqual("500", wanted.Sources.Single().ExternalKey);
        }

        [TestMethod]
        public void Apply_SetCodeUnderKnownPrefixWithNoPrinting_ReportsUnmappedProduct()
        {
            var card = Card(1, "Synthetic Dragon");
            Printing(card, 10, "SYN-EN001", "Ultra Rare");

            var result = Run(State(card), Context(), Product(500, "Synthetic Token", "SYN-ENTKN", "Common"));

            Assert.AreEqual(1, result.ProductsUnmapped);
            Assert.AreEqual("tcg:500", result.Items.Single().SubjectKey);
        }

        [TestMethod]
        public void Apply_SetCodeUnderUnknownPrefix_IsIgnored()
        {
            var card = Card(1, "Synthetic Dragon");
            Printing(card, 10, "SYN-EN001", "Ultra Rare");

            var result = Run(State(card), Context(), Product(500, "Sealed Booster Box", "BOX-EN000", "Common"));

            Assert.AreEqual(1, result.ProductsIgnored);
            Assert.AreEqual(0, result.Items.Count);
        }

        private static CatalogDiffResult Run(CatalogState state, CatalogSyncContext context, params SourceTCGProduct[] products)
        {
            var result = new CatalogDiffResult();
            CatalogProductDiff.Apply(state, products, context, new CatalogSetResolver(state, context, result), result);
            return result;
        }
    }
}
