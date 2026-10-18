using CardCollector.Data.Models.Catalog;
using CardCollector.Models.Catalog;
using CardCollector.Rules.Catalog;
using static CardCollector.Tests.TestHelpers.CatalogTestData;

namespace CardCollector.Tests.Rules.Catalog
{
    [TestClass]
    public sealed class CatalogCardDiffTests
    {
        [TestMethod]
        public void Apply_BootstrapNewCard_CreatesCardWithAliasesAndPrintingsButNoInfoItems()
        {
            var observed = new ObservedCard(
                YamlCard("Synthetic Dragon", 500, 11111111, new SourcePrinting("SYN-EN001", "Synthetic Set", "Ultra Rare")),
                [YGOProDeckCard("Synthetic Dragon", 11111111, 500)]);

            var (state, result) = Run(new CatalogState(), observed, null, Context(isBootstrap: true));

            var card = state.Cards.Single();
            Assert.AreEqual(11111111, card.Passcode);
            Assert.AreEqual(500, card.KonamiID);
            Assert.AreEqual(2, card.Aliases.Count);
            Assert.AreEqual("SYN-EN001", card.Printings.Single().SetCode);
            Assert.AreEqual(1, card.Images.Count);
            Assert.AreEqual(1, result.CardsAdded);
            Assert.AreEqual(0, result.Items.Count);
        }

        [TestMethod]
        public void Apply_CardSourceMissing_DoesNotTouchFields()
        {
            var card = Card(1, "Synthetic Dragon", 500, 11111111);
            card.Description = "Old text.";
            var observed = new ObservedCard(null, [YGOProDeckCard("Synthetic Dragon", 11111111, 500)]);

            var (_, result) = Run(State(card), observed, card, Context(yamlYugi: false));

            Assert.AreEqual("Old text.", card.Description);
            Assert.IsFalse(result.Items.Any(i => i.Kind == CatalogReviewItemKind.CardTextChanged));
        }

        [TestMethod]
        public void Apply_ExistingPrintingNowListedOnlyAsVariant_IsNotRewritten()
        {
            var card = Card(1, "Synthetic Dragon", 500, 11111111);
            var plain = Printing(card, 10, "RA05-EN146", "Ultra Rare");
            var observed = new ObservedCard(YamlCard("Synthetic Dragon", 500, 11111111, new SourcePrinting("RA05-EN146", "Rarity Collection 5", "Ultra Rare")), []);
            var products = new[] { Product(687324, "Synthetic Dragon", "RA05-EN146", "Ultra Rare", "Extended Art") };

            Run(State(card), observed, card, Context(), products);

            Assert.IsNull(plain.PrintVariant);
            Assert.AreEqual(NowUTC, plain.LastSeenUTC);
            Assert.AreEqual(1, card.Printings.Count);
        }

        [TestMethod]
        public void Apply_LockedField_IsLeftAlone()
        {
            var card = Card(1, "Synthetic Dragon", 500, 11111111);
            card.Description = "My own wording.";
            card.LockedFields = CatalogCardField.DESCRIPTION;
            var observed = new ObservedCard(new SourceCard { Description = "Source wording.", KonamiID = 500, Name = "Synthetic Dragon", Passcode = 11111111 }, []);

            var (_, result) = Run(State(card), observed, card, Context());

            Assert.AreEqual("My own wording.", card.Description);
            Assert.IsFalse(result.Items.Any(i => i.Kind == CatalogReviewItemKind.CardTextChanged));
        }

        [TestMethod]
        public void Apply_NameChanged_RecordsItemWithoutRenaming()
        {
            var card = Card(1, "Synthetic Realm", 500, 11111111);
            var observed = new ObservedCard(YamlCard("Synthetic Synchro Realm", 500, 11111111), []);

            var (_, result) = Run(State(card), observed, card, Context());

            Assert.AreEqual("Synthetic Realm", card.Name);
            Assert.AreEqual(CatalogReviewItemKind.CardNameChanged, result.Items.Single(i => i.Kind.IsActionable()).Kind);
        }

        [TestMethod]
        public void Apply_NewAliases_RecordsOneItemPerCard()
        {
            var card = Card(1, "Synthetic Beast", 23513, 52245126, (CatalogSource.YamlYugi, CatalogAliasKind.Passcode, 52245126));
            var observed = new ObservedCard(
                YamlCard("Synthetic Beast", 23513, 52245126),
                [YGOProDeckCard("Synthetic Beast", 52245126, 23513, betaID: 101402101)]);

            var (_, result) = Run(State(card), observed, card, Context());

            Assert.AreEqual(3, card.Aliases.Count);
            Assert.AreEqual(1, result.Items.Count(i => i.Kind == CatalogReviewItemKind.AliasAdded));
        }

        [TestMethod]
        public void Apply_PasscodeMissing_AssignsItAutomatically()
        {
            var card = Card(1, "Synthetic Beast", 23513, passcode: null);
            var observed = new ObservedCard(YamlCard("Synthetic Beast", 23513, 52245126), []);

            var (_, result) = Run(State(card), observed, card, Context());

            Assert.AreEqual(52245126, card.Passcode);
            Assert.IsTrue(result.Items.Any(i => i.Kind == CatalogReviewItemKind.PasscodeAssigned));
        }

        [TestMethod]
        public void Apply_PasscodeReportedDifferently_RecordsItemWithoutChangingIt()
        {
            var card = Card(10, "Synthetic Theory", 21466, 48739627);
            var observed = new ObservedCard(YamlCard("Synthetic Theory", 21466, 74733322), []);

            var (_, result) = Run(State(card), observed, card, Context());

            Assert.AreEqual(48739627, card.Passcode);
            var item = result.Items.Single(i => i.Kind == CatalogReviewItemKind.PasscodeChanged);
            Assert.AreSame(card, item.Card);
        }

        [TestMethod]
        public void Apply_TextChanged_AppliesItAndRecordsInfoItem()
        {
            var card = Card(1, "Synthetic Dragon", 500, 11111111, (CatalogSource.YamlYugi, CatalogAliasKind.Passcode, 11111111));
            card.Description = "Old text.";
            var observed = new ObservedCard(new SourceCard { Description = "New text.", KonamiID = 500, Name = "Synthetic Dragon", Passcode = 11111111 }, []);

            var (_, result) = Run(State(card), observed, card, Context());

            Assert.AreEqual("New text.", card.Description);
            Assert.AreEqual(1, result.CardsUpdated);
            Assert.AreEqual(CatalogReviewItemKind.CardTextChanged, result.Items.Single().Kind);
        }

        [TestMethod]
        public void ChooseInitialVariant_OnlyVariantProducts_ReturnsFirstVariant()
        {
            var products = new[]
            {
                Product(2, "Synthetic Dragon", "RA05-EN146", "Ultra Rare", "Extended Art"),
                Product(1, "Synthetic Dragon", "RA05-EN146", "Ultra Rare", "Alternate Art")
            };

            var variant = CatalogCardDiff.ChooseInitialVariant("Synthetic Dragon", products);

            Assert.AreEqual("Alternate Art", variant);
        }

        [TestMethod]
        public void ChooseInitialVariant_PlainProductListed_ReturnsNull()
        {
            var products = new[]
            {
                Product(1, "Synthetic Dragon", "SYN-EN001", "Ultra Rare"),
                Product(2, "Synthetic Dragon", "SYN-EN001", "Ultra Rare", "Extended Art")
            };

            var variant = CatalogCardDiff.ChooseInitialVariant("Synthetic Dragon", products);

            Assert.IsNull(variant);
        }

        [TestMethod]
        public void ChooseInitialVariant_ProductsOnlyForAnotherCard_ReturnsNull()
        {
            var products = new[]
            {
                Product(1, "Synthetic Other", "SYN-EN001", "Ultra Rare", "Extended Art"),
                Product(2, "Synthetic Third", "SYN-EN001", "Ultra Rare", "Extended Art")
            };

            var variant = CatalogCardDiff.ChooseInitialVariant("Synthetic Dragon", products);

            Assert.IsNull(variant);
        }

        private static (CatalogState State, CatalogDiffResult Result) Run(
            CatalogState state,
            ObservedCard observed,
            CatalogCard? existing,
            CatalogSyncContext context,
            IReadOnlyList<SourceTCGProduct>? products = null)
        {
            var result = new CatalogDiffResult();
            var match = new CatalogCardMatchResult { Matches = [(observed, existing)] };
            CatalogCardDiff.Apply(state, match, products ?? [], context, new CatalogSetResolver(state, context, result), result);
            return (state, result);
        }
    }
}
