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
        [DataRow(CatalogReviewItemKind.LegacyDismissalUnmapped, CatalogReviewItemStatus.Dismissed, DisplayName = "Dismissed legacy dismissal")]
        [DataRow(CatalogReviewItemKind.LegacyRowAutoMapped, CatalogReviewItemStatus.Dismissed, DisplayName = "Dismissed auto-mapped row")]
        [DataRow(CatalogReviewItemKind.LegacyRowUnmapped, CatalogReviewItemStatus.Accepted, DisplayName = "Restored row")]
        [DataRow(CatalogReviewItemKind.LegacyRowUnmapped, CatalogReviewItemStatus.Rejected, DisplayName = "Discarded row")]
        public void AllowedActions_ClosedLegacyItem_AllowsNothing(CatalogReviewItemKind kind, CatalogReviewItemStatus status)
        {
            var actions = CatalogReviewPresenter.AllowedActions(kind, status);

            Assert.AreEqual(0, actions.Count);
        }

        [TestMethod]
        [DataRow(CatalogReviewItemKind.PasscodeChanged, CatalogReviewAction.Accept, DisplayName = "Passcode change can be accepted")]
        [DataRow(CatalogReviewItemKind.UnmappedTCGProduct, CatalogReviewAction.MapToPrinting, DisplayName = "Unmapped product can be mapped")]
        [DataRow(CatalogReviewItemKind.CardAdded, CatalogReviewAction.Dismiss, DisplayName = "Informational item can be dismissed")]
        [DataRow(CatalogReviewItemKind.SyncAnomaly, CatalogReviewAction.Dismiss, DisplayName = "Anomaly can be dismissed")]
        public void AllowedActions_OpenItem_IncludesExpectedAction(CatalogReviewItemKind kind, CatalogReviewAction expected)
        {
            var actions = CatalogReviewPresenter.AllowedActions(kind, CatalogReviewItemStatus.Open);

            CollectionAssert.Contains(actions.ToList(), expected);
        }

        [TestMethod]
        [DataRow(CatalogReviewItemKind.LegacyDismissalUnmapped, DisplayName = "Legacy dismissal")]
        [DataRow(CatalogReviewItemKind.LegacyRowAutoMapped, DisplayName = "Auto-mapped row")]
        [DataRow(CatalogReviewItemKind.LegacyRowUnmapped, DisplayName = "Unmapped row")]
        public void AllowedActions_OpenLegacyItem_AllowsOnlyDismiss(CatalogReviewItemKind kind)
        {
            var actions = CatalogReviewPresenter.AllowedActions(kind, CatalogReviewItemStatus.Open);

            CollectionAssert.AreEqual(new[] { CatalogReviewAction.Dismiss }, actions.ToArray());
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
        public void ChoosePrimaryAction_AcceptRejectKind_ReturnsAccept()
        {
            var result = CatalogReviewPresenter.ChoosePrimaryAction([CatalogReviewAction.Accept, CatalogReviewAction.Reject], NoPlacement());

            Assert.AreEqual(CatalogReviewAction.Accept, result);
        }

        [TestMethod]
        public void ChoosePrimaryAction_ClosedItem_ReturnsReopen()
        {
            var result = CatalogReviewPresenter.ChoosePrimaryAction([CatalogReviewAction.Reopen], NoPlacement());

            Assert.AreEqual(CatalogReviewAction.Reopen, result);
        }

        [TestMethod]
        public void ChoosePrimaryAction_ExactMatchUnderSetCode_ReturnsMap()
        {
            var placement = new CatalogReviewPlacement([Option(10)], [Option(1)], "SYN-EN001", SetCodeInCatalog: true, HasExactMatch: true);

            var result = CatalogReviewPresenter.ChoosePrimaryAction(UnmappedActions(), placement);

            Assert.AreEqual(CatalogReviewAction.MapToPrinting, result);
        }

        [TestMethod]
        public void ChoosePrimaryAction_InformationalItem_ReturnsDismiss()
        {
            var result = CatalogReviewPresenter.ChoosePrimaryAction([CatalogReviewAction.Dismiss], NoPlacement());

            Assert.AreEqual(CatalogReviewAction.Dismiss, result);
        }

        [TestMethod]
        public void ChoosePrimaryAction_NoActions_ReturnsNull()
        {
            var result = CatalogReviewPresenter.ChoosePrimaryAction([], NoPlacement());

            Assert.IsNull(result);
        }

        [TestMethod]
        public void ChoosePrimaryAction_NoPlacementButCardCanBeCreated_ReturnsCreateCard()
        {
            var result = CatalogReviewPresenter.ChoosePrimaryAction(UnmappedActions(), NoPlacement());

            Assert.AreEqual(CatalogReviewAction.CreateCard, result);
        }

        [TestMethod]
        public void ChoosePrimaryAction_OnlyInexactPrintings_ReturnsCreatePrinting()
        {
            var placement = new CatalogReviewPlacement([Option(10)], [Option(1)], "SYN-EN001", SetCodeInCatalog: true, HasExactMatch: false);
            var actions = new[] { CatalogReviewAction.MapToPrinting, CatalogReviewAction.CreatePrinting, CatalogReviewAction.Reject };

            var result = CatalogReviewPresenter.ChoosePrimaryAction(actions, placement);

            Assert.AreEqual(CatalogReviewAction.CreatePrinting, result);
        }

        [TestMethod]
        public void ChoosePrimaryAction_OnlyInexactPrintingsAndNoCardToCreateOn_ReturnsMap()
        {
            var placement = new CatalogReviewPlacement([Option(10)], [], "SYN-EN001", SetCodeInCatalog: true, HasExactMatch: false);
            var actions = new[] { CatalogReviewAction.MapToPrinting, CatalogReviewAction.CreatePrinting, CatalogReviewAction.Reject };

            var result = CatalogReviewPresenter.ChoosePrimaryAction(actions, placement);

            Assert.AreEqual(CatalogReviewAction.MapToPrinting, result);
        }

        [TestMethod]
        public void ChoosePrimaryAction_SetCodeNotInCatalog_ReturnsCreatePrinting()
        {
            var placement = new CatalogReviewPlacement([Option(10)], [Option(1)], "NEW-EN001", SetCodeInCatalog: false);

            var result = CatalogReviewPresenter.ChoosePrimaryAction(UnmappedActions(), placement);

            Assert.AreEqual(CatalogReviewAction.CreatePrinting, result);
        }

        [TestMethod]
        public void ToViewModel_CardNameChange_HeadlineNamesTheReportedName()
        {
            var item = Item(CatalogReviewItemKind.CardNameChanged, """{"Current":"Synthetic Dragon","Reported":"Synthetic Dragon (Updated)"}""");

            var model = CatalogReviewPresenter.ToViewModel(item, Lookup());

            Assert.AreEqual("Now reported as \"Synthetic Dragon (Updated)\"", model.Headline);
        }

        [TestMethod]
        public void ToViewModel_KindWithoutHeadline_HeadlineIsTheSummary()
        {
            var item = Item(CatalogReviewItemKind.SyncAnomaly, """{"Source":"YamlYugi"}""");

            var model = CatalogReviewPresenter.ToViewModel(item, Lookup());

            Assert.AreEqual("Synthetic summary", model.Headline);
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
        public void ToViewModel_PasscodeChange_HeadlineNamesBothPasscodes()
        {
            var item = Item(CatalogReviewItemKind.PasscodeChanged, """{"CardName":"Synthetic Theory","Current":48739627,"Reported":74733322}""");

            var model = CatalogReviewPresenter.ToViewModel(item, Lookup());

            Assert.AreEqual("Passcode reported as 74733322, not 48739627", model.Headline);
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
        public void ToViewModel_PasscodeChangeMissingCurrent_HeadlineIsTheSummary()
        {
            var item = Item(CatalogReviewItemKind.PasscodeChanged, """{"CardName":"Synthetic Theory","Reported":74733322}""");

            var model = CatalogReviewPresenter.ToViewModel(item, Lookup());

            Assert.AreEqual("Synthetic summary", model.Headline);
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
        public void ToViewModel_ProductNamingOneCard_ShowsThatCardsArtAndLinksToIt()
        {
            var card = Card(9, "Synthetic God", 10000000, Printing(90, "JUMP-EN037", "Ultra Rare"));
            card.CardImages = [new Image { ImageURLSmall = "https://images.example/synthetic-god.jpg" }];
            var item = Item(CatalogReviewItemKind.UnmappedTCGProduct, """{"ProductID":23199,"CardName":"Synthetic God","SetCode":"SYN-001","RarityName":"Prismatic Secret Rare"}""");

            var model = CatalogReviewPresenter.ToViewModel(item, Lookup(card));

            Assert.AreEqual("https://images.example/synthetic-god.jpg", model.ImageURLSmall);
            Assert.AreEqual(9, model.DisplayCardID);
            Assert.IsNull(model.CatalogCardID);
        }

        [TestMethod]
        public void ToViewModel_ProductNamingTwoCards_ShowsNoArt()
        {
            var first = Card(1, "Synthetic Twin", 11111111, Printing(10, "OLD-001", "Rare"));
            var second = Card(2, "Synthetic Twin", 22222222, Printing(20, "OLD-002", "Rare"));
            first.CardImages = [new Image { ImageURLSmall = "https://images.example/twin-1.jpg" }];
            second.CardImages = [new Image { ImageURLSmall = "https://images.example/twin-2.jpg" }];
            var item = Item(CatalogReviewItemKind.UnmappedTCGProduct, """{"ProductID":1,"CardName":"Synthetic Twin","SetCode":"NEW-001","RarityName":"Rare"}""");

            var model = CatalogReviewPresenter.ToViewModel(item, Lookup(first, second));

            Assert.IsNull(model.ImageURLSmall);
            Assert.IsNull(model.DisplayCardID);
        }

        [TestMethod]
        public void ToViewModel_ProductWhoseSetCodeNoCardHas_OffersPrintingsOfTheNamedCard()
        {
            var card = Card(9, "Synthetic God", 10000000, Printing(90, "JUMP-EN037", "Ultra Rare"));
            var item = Item(CatalogReviewItemKind.UnmappedTCGProduct,
                """{"ProductID":79079,"CardName":"Synthetic God","SetCode":"LC01-EN001","RarityName":"Ultra Rare","PrintVariant":null}""");

            var model = CatalogReviewPresenter.ToViewModel(item, Lookup(card));

            Assert.AreEqual(90, model.PrintingOptions.Single().Value);
            Assert.AreEqual("JUMP-EN037 Ultra Rare", model.PrintingOptions.Single().Label);
            Assert.AreEqual(9, model.CardOptions.Single().Value);
            Assert.AreEqual("LC01-EN001", model.PlacementSetCode);
            Assert.IsFalse(model.PlacementSetCodeInCatalog);
            Assert.AreEqual(CatalogReviewAction.CreatePrinting, model.PrimaryAction);
        }

        [TestMethod]
        public void ToViewModel_ProductWithExactAndOtherPrintingsUnderItsSetCode_ListsTheExactMatchFirstAndFlagsTheRest()
        {
            var card = Card(5, "Synthetic Dragon", 11111111,
                Printing(51, "SYN-EN001", "Common"),
                Printing(52, "SYN-EN001", "Ultra Rare", "Alternate Art"),
                Printing(53, "SYN-EN001", "Ultra Rare"));
            var item = Item(CatalogReviewItemKind.UnmappedTCGProduct, """{"ProductID":1,"CardName":"Synthetic Dragon","SetCode":"SYN-EN001","RarityName":"Ultra Rare"}""");

            var model = CatalogReviewPresenter.ToViewModel(item, Lookup(card));

            CollectionAssert.AreEqual(
                new[] { "SYN-EN001 Ultra Rare", "SYN-EN001 Ultra Rare (Alternate Art) — different variant", "SYN-EN001 Common — different rarity" },
                model.PrintingOptions.Select(o => o.Label).ToArray());
            Assert.IsTrue(model.PlacementSetCodeInCatalog);
            Assert.AreEqual(CatalogReviewAction.MapToPrinting, model.PrimaryAction);
        }

        [TestMethod]
        public void ToViewModel_ProductWithOnlyOtherRaritiesUnderItsSetCode_LeadsWithCreatePrinting()
        {
            var card = Card(5, "Synthetic Dragon", 11111111, Printing(51, "SYN-EN001", "Common"));
            var item = Item(CatalogReviewItemKind.UnmappedTCGProduct, """{"ProductID":1,"CardName":"Synthetic Dragon","SetCode":"SYN-EN001","RarityName":"Ultra Rare"}""");

            var model = CatalogReviewPresenter.ToViewModel(item, Lookup(card));

            Assert.AreEqual(CatalogReviewAction.CreatePrinting, model.PrimaryAction);
        }

        [TestMethod]
        public void ToViewModel_ResolvedLegacyRow_ShowsItsValuesWithoutPlacementOrHiddenColumns()
        {
            var card = Card(5, "Synthetic Duelist", 37440988, Printing(51, "LDS1-EN101", "Ultra Rare", "Blue"));
            var item = Item(CatalogReviewItemKind.LegacyRowUnmapped,
                """{"Table":"CollectionEntries","Row":{"ID":4331,"CardID":37440988,"SetCode":"LDS1-EN101","RarityName":"Ultra Rare","DateCreated":"2026-06-13"},"Reason":"No match"}""");
            item.Status = CatalogReviewItemStatus.Accepted;

            var model = CatalogReviewPresenter.ToViewModel(item, Lookup(card));

            Assert.AreEqual(0, model.Actions.Count);
            Assert.AreEqual(0, model.PrintingOptions.Count);
            Assert.IsNull(model.RejectLabel);
            var row = model.Details.Single(d => d.Key == "Row").Value;
            StringAssert.Contains(row, "SetCode LDS1-EN101");
            Assert.IsFalse(row.Contains("DateCreated"));
        }

        [TestMethod]
        public void ToViewModel_UnknownPayloadProperty_LabelSplitsTheNameIntoWords()
        {
            var item = Item(CatalogReviewItemKind.CardMatchConflict, """{"ObservedName":"Synthetic Drake","ObservedKonamiID":4321}""");

            var model = CatalogReviewPresenter.ToViewModel(item, Lookup());

            CollectionAssert.AreEqual(new[] { "Observed Name", "Observed Konami ID" }, model.Details.Select(d => d.Key).ToArray());
        }

        [TestMethod]
        public void ToViewModel_UnmappedProduct_HeadlineIsTheCapitalizedReason()
        {
            var item = Item(CatalogReviewItemKind.UnmappedTCGProduct, """{"ProductID":108329,"CardName":"Synthetic God","SetCode":"SYN-ENG01","RarityName":"Ultra Rare","Reason":"no catalog printing has this set code"}""");

            var model = CatalogReviewPresenter.ToViewModel(item, Lookup());

            Assert.AreEqual("No catalog printing has this set code", model.Headline);
        }

        [TestMethod]
        public void ToViewModel_UnmappedProduct_ReadsChipFieldsAndLeavesThemOutOfTheDetails()
        {
            var item = Item(CatalogReviewItemKind.UnmappedTCGProduct,
                """{"ProductID":108329,"CardName":"Synthetic God","SetCode":"SYN-ENG01","RarityName":"Ultra Rare","PrintVariant":"Alternate Art","Reason":"no catalog printing has this set code"}""");

            var model = CatalogReviewPresenter.ToViewModel(item, Lookup());

            Assert.AreEqual(108329, model.ProductID);
            Assert.AreEqual("SYN-ENG01", model.SetCode);
            Assert.AreEqual("Ultra Rare", model.RarityName);
            Assert.AreEqual("Alternate Art", model.PrintVariant);
            Assert.AreEqual(0, model.Details.Count);
        }

        private static Card Card(int id, string name, int passcode, params Set[] printings) =>
            new() { CardSets = printings, ID = id, Name = name, Passcode = passcode };

        private static CatalogReviewItem Item(CatalogReviewItemKind kind, string payloadJSON) => new()
        {
            ID = 1,
            Kind = kind,
            PayloadJSON = payloadJSON,
            Status = CatalogReviewItemStatus.Open,
            SubjectKey = "subject",
            Summary = "Synthetic summary"
        };

        private static CatalogReviewLookup Lookup(params Card[] cards) => new()
        {
            CardByID = id => cards.FirstOrDefault(c => c.ID == id),
            CardsByName = name => cards.Where(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)).ToList(),
            PrintingsBySetCode = setCode => cards
                .SelectMany(c => (c.CardSets ?? []).Select(s => new CatalogPrintingRef(c, s)))
                .Where(r => string.Equals(r.Printing.Code, setCode, StringComparison.OrdinalIgnoreCase))
                .ToList()
        };

        private static CatalogReviewPlacement NoPlacement() => new([], [], null, false);

        private static CatalogReviewOption Option(int value) => new(value, $"Option {value}");

        private static Set Printing(int printingID, string setCode, string rarity, string? variant = null) =>
            new() { Code = setCode, PrintingID = printingID, PrintVariant = variant, RarityName = rarity };

        private static CatalogReviewAction[] UnmappedActions() =>
            [CatalogReviewAction.MapToPrinting, CatalogReviewAction.CreatePrinting, CatalogReviewAction.CreateCard, CatalogReviewAction.Reject];
    }
}
