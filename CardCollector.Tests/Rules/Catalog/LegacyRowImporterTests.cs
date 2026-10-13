using CardCollector.Data.Models;
using CardCollector.Data.Models.Catalog;
using CardCollector.Models.Catalog;
using CardCollector.Models.Legacy;
using CardCollector.Rules.Catalog;
using static CardCollector.Tests.TestHelpers.CatalogTestData;

namespace CardCollector.Tests.Rules.Catalog
{
    [TestClass]
    public sealed class LegacyRowImporterTests
    {
        private const string LEGACY_DATE = "2026-06-14 23:14:42.8939599";

        private IList<DetectedReviewItem> _items = null!;
        private LegacyMigrationReport _report = null!;

        [TestMethod]
        public void ImportCheckedOutCards_TwoRowsOnOnePrinting_SumsQuantities()
        {
            var importer = Importer();
            var rows = new[] { CheckedOutRow(1, "Secret Rare", "Secret", 1), CheckedOutRow(2, "Secret Rare", null, 2) };

            var entities = importer.ImportCheckedOutCards(rows);

            Assert.AreEqual(3, entities.Single().Quantity);
            Assert.AreEqual(1, _report.Tables.Single().Merged);
        }

        [TestMethod]
        public void ImportCollectionEntries_ExactRow_KeepsIDAndPointsAtCatalog()
        {
            var importer = Importer();

            var entry = importer.ImportCollectionEntries([EntryRow(2097, 11111111, "SYN-EN001", "Ultra Rare")]).Single();

            Assert.AreEqual(2097, entry.ID);
            Assert.AreEqual(1, entry.CardID);
            Assert.AreEqual(10, entry.CatalogPrintingID);
            Assert.AreEqual(CardEdition.FirstEdition, entry.Edition);
            Assert.AreEqual(CollectionStatus.Owned, entry.Status);
            Assert.AreEqual(0, _items.Count);
        }

        [TestMethod]
        public void ImportCollectionEntries_UnmappedRow_BecomesActionableItemAndIsNotWritten()
        {
            var importer = Importer();

            var entries = importer.ImportCollectionEntries([EntryRow(5, 11111111, "GONE-EN001", "Rare")]);

            Assert.AreEqual(0, entries.Count);
            var item = _items.Single();
            Assert.AreEqual(CatalogReviewItemKind.LegacyRowUnmapped, item.Kind);
            Assert.AreEqual("legacy:CollectionEntries:5", item.SubjectKey);
            Assert.AreEqual(1, _report.UnmappedRows.Count);
        }

        [TestMethod]
        public void ImportCollectionEntries_VariantFilledRow_RecordsInfoItem()
        {
            var importer = Importer();

            var entry = importer.ImportCollectionEntries([EntryRow(9, 11111111, "SYN-EN002", "Ultra Rare")]).Single();

            Assert.AreEqual("Extended Art", entry.PrintVariant);
            Assert.AreEqual(CatalogReviewItemKind.LegacyRowAutoMapped, _items.Single().Kind);
        }

        [TestMethod]
        public void ImportDeckCards_KnownAndUnknownPasscodes_KeepPasscodeAndResolveWhenPossible()
        {
            var importer = Importer();
            var rows = new[] { DeckRow(1, 11111111), DeckRow(2, 424242) };

            var cards = importer.ImportDeckCards(rows);

            Assert.AreEqual(11111111, cards[0].CardID);
            Assert.AreEqual(1, cards[0].CatalogCardID);
            Assert.IsNull(cards[1].CatalogCardID);
        }

        [TestMethod]
        public void ImportDismissedNewPrintings_UnmappedRow_BecomesInformationalItem()
        {
            var importer = Importer();
            var row = Row(("ID", 3L), ("CardID", 11111111L), ("SetCode", "GONE-EN001"), ("RarityName", "Rare"), ("DateCreated", LEGACY_DATE), ("DateModified", LEGACY_DATE));

            importer.ImportDismissedNewPrintings([row]);

            Assert.AreEqual(CatalogReviewItemKind.LegacyDismissalUnmapped, _items.Single().Kind);
            Assert.IsFalse(_items.Single().Kind.IsActionable());
        }

        [TestMethod]
        public void ImportPreferredVersions_TwoRowsOnOnePrinting_KeepsHigherDesiredQuantity()
        {
            var importer = Importer();
            var rows = new[] { PreferredRow(1, "Starfoil", 2), PreferredRow(2, null, 3) };

            var versions = importer.ImportPreferredVersions(rows);

            Assert.AreEqual(1, versions.Single().ID);
            Assert.AreEqual(3, versions.Single().DesiredQuantity);
        }

        [TestInitialize]
        public void Initialize()
        {
            _items = new List<DetectedReviewItem>();
            _report = new LegacyMigrationReport();
        }

        private static LegacyRow CheckedOutRow(int id, string rarity, string? variant, int quantity) => Row(
            ("ID", (long)id), ("CardID", 11111111L), ("SetCode", "SYN-EN003"), ("RarityName", rarity), ("PrintVariant", variant),
            ("Quantity", (long)quantity), ("CheckedOutDate", LEGACY_DATE), ("DateCreated", LEGACY_DATE), ("DateModified", LEGACY_DATE));

        private static LegacyRow DeckRow(int id, int passcode) => Row(
            ("ID", (long)id), ("CardID", (long)passcode), ("DeckID", 1L), ("Quantity", 3L), ("Section", "Main"), ("SortOrder", 0L));

        private static LegacyRow EntryRow(int id, int cardID, string setCode, string rarity) => Row(
            ("ID", (long)id), ("CardID", (long)cardID), ("SetCode", setCode), ("RarityName", rarity), ("PrintVariant", null),
            ("Status", "Owned"), ("Condition", "NearMint"), ("Edition", "FirstEdition"), ("AcquisitionMethod", "Pulled"), ("Quantity", 3L),
            ("PurchaseDate", "2025-07-11 00:00:00"), ("PurchasePrice", null), ("MarketPriceAtEntry", 6.81), ("DateCreated", LEGACY_DATE), ("DateModified", LEGACY_DATE));

        private static LegacyRow PreferredRow(int id, string? variant, int desiredQuantity) => Row(
            ("ID", (long)id), ("CardID", 11111111L), ("SetCode", "SP18-EN013"), ("RarityName", "Starfoil Rare"), ("PrintVariant", variant),
            ("DesiredQuantity", (long)desiredQuantity), ("DateCreated", LEGACY_DATE), ("DateModified", LEGACY_DATE));

        private static LegacyRow Row(params (string Column, object? Value)[] values) =>
            new(values.ToDictionary(v => v.Column, v => v.Value, StringComparer.OrdinalIgnoreCase));

        private LegacyRowImporter Importer()
        {
            var card = Card(1, "Synthetic Dragon", passcode: 11111111, aliases: (CatalogSource.YamlYugi, CatalogAliasKind.Passcode, 11111111));
            Printing(card, 10, "SYN-EN001", "Ultra Rare");
            Printing(card, 11, "SYN-EN002", "Ultra Rare", "Extended Art");
            Printing(card, 12, "SYN-EN003", "Secret Rare");
            Printing(card, 13, "SP18-EN013", "Starfoil Rare");
            var index = LegacyRowMapper.BuildIndex(State(card), new Dictionary<(string, string), string>());
            return new LegacyRowImporter(index, _items, _report);
        }
    }
}
