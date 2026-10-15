using CardCollector.Data.Models.Catalog;
using CardCollector.Rules.Catalog;
using static CardCollector.Tests.TestHelpers.CatalogTestData;

namespace CardCollector.Tests.Rules.Catalog
{
    [TestClass]
    public sealed class CatalogReadModelBuilderTests
    {
        [TestMethod]
        public void Build_ActivePrinting_IsIndexedByIDWithItsCard()
        {
            var card = Card(42, "Synthetic Dragon");
            Printing(card, 7, "RA05-EN146", "Ultra Rare", "Extended Art");
            Printing(card, 8, "RA05-EN146", "Ultra Rare").Status = CatalogStatus.Retired;

            var model = CatalogReadModelBuilder.Build(State(card));

            var printing = model.PrintingsByID[7];
            Assert.AreEqual(42, printing.Card.ID);
            Assert.AreEqual("Extended Art", printing.Printing.PrintVariant);
            Assert.IsFalse(model.PrintingsByID.ContainsKey(8));
        }

        [TestMethod]
        public void Build_AnyPrinting_MapsToItsCardIDEvenWhenRetired()
        {
            var retiredCard = Card(1, "Synthetic Retired");
            retiredCard.Status = CatalogStatus.Retired;
            Printing(retiredCard, 10, "SYN-EN001", "Rare");
            var card = Card(2, "Synthetic Active");
            Printing(card, 20, "SYN-EN002", "Rare");
            Printing(card, 21, "SYN-EN002", "Super Rare").Status = CatalogStatus.Retired;

            var model = CatalogReadModelBuilder.Build(State(retiredCard, card));

            Assert.AreEqual(1, model.CardIDsByPrintingID[10]);
            Assert.AreEqual(2, model.CardIDsByPrintingID[20]);
            Assert.AreEqual(2, model.CardIDsByPrintingID[21]);
        }

        [TestMethod]
        public void Build_CardWithoutImages_FallsBackToPasscodeImage()
        {
            var card = Card(1, "Synthetic Dragon", passcode: 11111111);

            var model = CatalogReadModelBuilder.Build(State(card));

            StringAssert.Contains(model.CardsByID[1].CardImages![0].ImageURL, "11111111");
        }

        [TestMethod]
        public void Build_CardWithoutPrintings_IsNotBrowseable()
        {
            var card = Card(1, "Synthetic Announcement");

            var model = CatalogReadModelBuilder.Build(State(card));

            Assert.AreEqual(1, model.Cards.Count);
            Assert.AreEqual(0, model.BrowseableCards.Count);
        }

        [TestMethod]
        public void Build_CardWithPrintings_UsesCatalogIDsAndLinkedProducts()
        {
            var card = Card(42, "Synthetic Dragon", 500, 11111111);
            Printing(card, 7, "RA05-EN146", "Ultra Rare", "Extended Art", 687324);

            var model = CatalogReadModelBuilder.Build(State(card));

            var built = model.CardsByID[42];
            Assert.AreEqual(11111111, built.Passcode);
            Assert.AreEqual(500, built.KonamiID);
            var set = built.CardSets!.Single();
            Assert.AreEqual(7, set.PrintingID);
            Assert.AreEqual("Extended Art", set.PrintVariant);
            CollectionAssert.AreEqual(new[] { 687324 }, set.TCGProductIDs.ToArray());
            Assert.AreSame(built, model.BrowseableCards.Single());
        }

        [TestMethod]
        public void Build_RetiredCardAndPrinting_AreLeftOut()
        {
            var retired = Card(1, "Synthetic Retired");
            retired.Status = CatalogStatus.Retired;
            var active = Card(2, "Synthetic Active");
            Printing(active, 10, "SYN-EN001", "Rare").Status = CatalogStatus.Retired;

            var model = CatalogReadModelBuilder.Build(State(retired, active));

            Assert.IsFalse(model.CardsByID.ContainsKey(1));
            Assert.AreEqual(0, model.CardsByID[2].CardSets!.Count);
        }

        [TestMethod]
        public void BuildPasscodeAliases_SharedValue_GoesToStrongerClaimAndOwnPasscodeCounts()
        {
            var byArtwork = Card(1, "Synthetic Art Holder", passcode: 33333333, aliases: (CatalogSource.YGOProDeck, CatalogAliasKind.Artwork, 55555555));
            var byPasscode = Card(2, "Synthetic Owner", aliases: (CatalogSource.YamlYugi, CatalogAliasKind.Passcode, 55555555));

            var aliases = CatalogReadModelBuilder.BuildPasscodeAliases([byArtwork, byPasscode]);

            Assert.AreEqual(2, aliases[55555555]);
            Assert.AreEqual(1, aliases[33333333]);
        }
    }
}
