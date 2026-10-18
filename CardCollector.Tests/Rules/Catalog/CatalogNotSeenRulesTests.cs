using CardCollector.Data.Models.Catalog;
using CardCollector.Models.Catalog;
using CardCollector.Rules.Catalog;
using static CardCollector.Tests.TestHelpers.CatalogTestData;

namespace CardCollector.Tests.Rules.Catalog
{
    [TestClass]
    public sealed class CatalogNotSeenRulesTests
    {
        [TestMethod]
        public void Detect_CardNotSeenWithBothCardSources_ReportsIt()
        {
            var card = Card(1, "Synthetic Vanished");

            var result = Run(State(card), Context());

            Assert.AreEqual(CatalogReviewItemKind.CardNotSeen, result.Items.Single().Kind);
        }

        [TestMethod]
        public void Detect_CardOnlyOnTCGPlayerWhenTCGPlayerFailed_IsNotReported()
        {
            var card = Card(1, "Synthetic Legend");
            Printing(card, 10, "SYN-EN051", "Prismatic Secret Rare", null, 97893);

            var result = Run(State(card), Context(tcgPlayer: false));

            Assert.AreEqual(0, result.Items.Count);
        }

        [TestMethod]
        public void Detect_CardOnlyOnTCGPlayerWithProductSeen_IsNotReported()
        {
            var card = Card(1, "Synthetic Legend");
            var printing = Printing(card, 10, "SYN-EN051", "Prismatic Secret Rare", null, 97893);
            printing.Sources.Single().LastSeenUTC = NowUTC;

            var result = Run(State(card), Context());

            Assert.AreEqual(0, result.Items.Count);
        }

        [TestMethod]
        public void Detect_CardSourceFailed_ReportsNothing()
        {
            var card = Card(1, "Synthetic Vanished");

            var result = Run(State(card), Context(ygoProDeck: false));

            Assert.AreEqual(0, result.Items.Count);
        }

        [TestMethod]
        public void Detect_CardWhoseProductsAreAlsoGone_ReportsIt()
        {
            var card = Card(1, "Synthetic Legend");
            Printing(card, 10, "SYN-EN051", "Prismatic Secret Rare", null, 97893);

            var result = Run(State(card), Context());

            Assert.AreEqual(CatalogReviewItemKind.CardNotSeen, result.Items.Single().Kind);
        }

        [TestMethod]
        public void Detect_ManualPrintingNotSeen_IsNotReported()
        {
            var card = Card(1, "Synthetic Dragon");
            card.LastSeenUTC = NowUTC;
            var printing = Printing(card, 10, "SYN-EN001", "Ultra Rare");
            printing.Sources.Add(new CatalogPrintingSource { ExternalKey = "manual:10", Source = CatalogSource.Manual });

            var result = Run(State(card), Context());

            Assert.AreEqual(0, result.Items.Count);
        }

        [TestMethod]
        public void Detect_PrintingNotSeenOnSeenCard_ReportsPrintingOnlyWhenTCGPlayerSucceeded()
        {
            var card = Card(1, "Synthetic Dragon");
            card.LastSeenUTC = NowUTC;
            var printing = Printing(card, 10, "SYN-EN001", "Ultra Rare");

            var withTCG = Run(State(card), Context());
            var withoutTCG = Run(State(card), Context(tcgPlayer: false));

            Assert.AreSame(printing, withTCG.Items.Single(i => i.Kind == CatalogReviewItemKind.PrintingNotSeen).Printing);
            Assert.AreEqual(0, withoutTCG.Items.Count);
        }

        [TestMethod]
        public void Detect_RetiredCard_IsNotReported()
        {
            var card = Card(1, "Synthetic Retired");
            card.Status = CatalogStatus.Retired;

            var result = Run(State(card), Context());

            Assert.AreEqual(0, result.Items.Count);
        }

        private static CatalogDiffResult Run(CatalogState state, CatalogSyncContext context)
        {
            var result = new CatalogDiffResult();
            CatalogNotSeenRules.Detect(state, context, result);
            return result;
        }
    }
}
