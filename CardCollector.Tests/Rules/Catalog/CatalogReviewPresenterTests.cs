using CardCollector.Data.Models.Catalog;
using CardCollector.DTO;
using CardCollector.Models.Catalog;
using CardCollector.Rules.Catalog;
using CardCollector.ViewModels;

namespace CardCollector.Tests.Rules.Catalog
{
    [TestClass]
    public sealed class CatalogReviewPresenterTests
    {
        [TestMethod]
        [DataRow(CatalogReviewItemStatus.Accepted, DisplayName = "Accepted")]
        [DataRow(CatalogReviewItemStatus.ResolvedBySource, DisplayName = "Resolved by source")]
        public void AllowedActions_AppliedItem_AllowsNothing(CatalogReviewItemStatus status)
        {
            var actions = CatalogReviewPresenter.AllowedActions(CatalogReviewItemKind.PasscodeChanged, status);

            Assert.AreEqual(0, actions.Count);
        }

        [TestMethod]
        [DataRow(CatalogReviewItemKind.PasscodeChanged, CatalogReviewAction.Accept, DisplayName = "Passcode change can be accepted")]
        [DataRow(CatalogReviewItemKind.UnmappedTCGProduct, CatalogReviewAction.MapToPrinting, DisplayName = "Unmapped product can be mapped")]
        [DataRow(CatalogReviewItemKind.LegacyRowUnmapped, CatalogReviewAction.CreatePrinting, DisplayName = "Legacy row can get a new printing")]
        [DataRow(CatalogReviewItemKind.CardAdded, CatalogReviewAction.Dismiss, DisplayName = "Informational item can be dismissed")]
        [DataRow(CatalogReviewItemKind.SyncAnomaly, CatalogReviewAction.Dismiss, DisplayName = "Anomaly can be dismissed")]
        public void AllowedActions_OpenItem_IncludesExpectedAction(CatalogReviewItemKind kind, CatalogReviewAction expected)
        {
            var actions = CatalogReviewPresenter.AllowedActions(kind, CatalogReviewItemStatus.Open);

            CollectionAssert.Contains(actions.ToList(), expected);
        }

        [TestMethod]
        [DataRow(CatalogReviewItemStatus.Rejected, DisplayName = "Rejected")]
        [DataRow(CatalogReviewItemStatus.Dismissed, DisplayName = "Dismissed")]
        public void AllowedActions_RejectedOrDismissedItem_AllowsOnlyReopen(CatalogReviewItemStatus status)
        {
            var actions = CatalogReviewPresenter.AllowedActions(CatalogReviewItemKind.UnmappedTCGProduct, status);

            CollectionAssert.AreEqual(new[] { CatalogReviewAction.Reopen }, actions.ToArray());
        }

        [TestMethod]
        public void ToViewModel_LegacyRowWithSeveralPrintingsUnderItsSetCode_OffersEachPrinting()
        {
            var card = Card(5, "Synthetic Duelist", 37440988, Printing(51, "LDS1-EN101", "Ultra Rare", "Blue"), Printing(52, "LDS1-EN101", "Ultra Rare", "Green"));
            var item = Item(CatalogReviewItemKind.LegacyRowUnmapped,
                """{"Table":"CollectionEntries","Row":{"ID":4331,"CardID":37440988,"SetCode":"LDS1-EN101","RarityName":"Ultra Rare","DateCreated":"2026-06-13"},"Reason":"No match"}""");

            var model = CatalogReviewPresenter.ToViewModel(item, Lookup(card));

            CollectionAssert.AreEquivalent(new[] { 51, 52 }, model.PrintingOptions.Select(o => o.Value).ToArray());
            Assert.AreEqual("LDS1-EN101 Ultra Rare (Blue)", model.PrintingOptions.Single(o => o.Value == 51).Label);
            Assert.AreEqual(5, model.CardOptions.Single().Value);
            Assert.AreEqual("Discard row", model.RejectLabel);
            Assert.IsFalse(model.Details.Single(d => d.Key == "Row").Value.Contains("DateCreated"));
        }

        [TestMethod]
        public void ToViewModel_OptionsFromSeveralCards_LabelsIncludeCardName()
        {
            var dragon = Card(1, "Synthetic Dragon", 11111111, Printing(10, "OLD-001", "Rare"));
            var wyvern = Card(2, "Synthetic Wyvern", 22222222, Printing(20, "OLD-001", "Rare"));
            var item = Item(CatalogReviewItemKind.UnmappedTCGProduct, """{"ProductID":1,"CardName":"Synthetic Drake","SetCode":"OLD-001","RarityName":"Rare"}""");

            var model = CatalogReviewPresenter.ToViewModel(item, Lookup(dragon, wyvern));

            Assert.AreEqual("Synthetic Dragon — OLD-001 Rare", model.PrintingOptions.Single(o => o.Value == 10).Label);
        }

        [TestMethod]
        public void ToViewModel_PasscodeChange_LabelsButtonsWithBothPasscodes()
        {
            var item = Item(CatalogReviewItemKind.PasscodeChanged, """{"CardName":"Synthetic Theory","Current":48739627,"Reported":74733322}""");

            var model = CatalogReviewPresenter.ToViewModel(item, Lookup());

            Assert.AreEqual("Use 74733322", model.AcceptLabel);
            Assert.AreEqual("Keep 48739627", model.RejectLabel);
            Assert.AreEqual("Synthetic Theory", model.CardName);
            Assert.IsTrue(model.IsActionable);
        }

        [TestMethod]
        public void ToViewModel_ProductNamingExistingCard_DoesNotOfferCreateCard()
        {
            var card = Card(5, "Synthetic Dragon", 11111111, Printing(51, "SYN-EN001", "Ultra Rare"));
            var item = Item(CatalogReviewItemKind.UnmappedTCGProduct, """{"ProductID":97893,"CardName":"synthetic dragon","SetCode":"SYN-EN051","RarityName":"Secret Rare"}""");

            var model = CatalogReviewPresenter.ToViewModel(item, Lookup(card));

            CollectionAssert.DoesNotContain(model.Actions.ToList(), CatalogReviewAction.CreateCard);
        }

        [TestMethod]
        public void ToViewModel_ProductNamingNoCatalogCard_OffersCreateCard()
        {
            var item = Item(CatalogReviewItemKind.UnmappedTCGProduct, """{"ProductID":97893,"CardName":"Synthetic Legend","SetCode":"SYN-EN051","RarityName":"Secret Rare"}""");

            var model = CatalogReviewPresenter.ToViewModel(item, Lookup());

            CollectionAssert.Contains(model.Actions.ToList(), CatalogReviewAction.CreateCard);
        }

        [TestMethod]
        public void ToViewModel_ProductWhoseSetCodeNoCardHas_OffersPrintingsOfTheNamedCard()
        {
            var card = Card(9, "Synthetic God", 10000000, Printing(90, "JUMP-EN037", "Ultra Rare"));
            var item = Item(CatalogReviewItemKind.UnmappedTCGProduct,
                """{"ProductID":79079,"CardName":"Synthetic God","SetCode":"LC01-EN001","RarityName":"Ultra Rare","PrintVariant":null}""");

            var model = CatalogReviewPresenter.ToViewModel(item, Lookup(card));

            Assert.AreEqual(90, model.PrintingOptions.Single().Value);
            Assert.AreEqual(9, model.CardOptions.Single().Value);
        }

        private static Card Card(int id, string name, int passcode, params Set[] printings) =>
            new() { CardSets = printings, ID = id, Name = name, Passcode = passcode };

        private static CatalogReviewItem Item(CatalogReviewItemKind kind, string payloadJson) => new()
        {
            ID = 1,
            Kind = kind,
            PayloadJson = payloadJson,
            Status = CatalogReviewItemStatus.Open,
            SubjectKey = "subject",
            Summary = "Synthetic summary"
        };

        private static CatalogReviewLookup Lookup(params Card[] cards) =>
            CatalogReviewPresenter.BuildLookup(cards, passcode => cards.FirstOrDefault(c => c.Passcode == passcode));

        private static Set Printing(int printingID, string setCode, string rarity, string? variant = null) =>
            new() { Code = setCode, PrintingID = printingID, PrintVariant = variant, RarityName = rarity };
    }
}
