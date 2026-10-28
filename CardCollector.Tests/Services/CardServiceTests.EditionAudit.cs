using CardCollector.Data.Models;
using CardCollector.DTO;
using CardCollector.ViewModels;
using Moq;

namespace CardCollector.Tests.Services
{
    public partial class CardServiceTests
    {
        [TestMethod]
        public async Task CheckEntryEditionAsync_PrintingLeftTheCatalog_ReturnsUnverifiable()
        {
            var result = await _service.CheckEntryEditionAsync(999, CardEdition.FirstEdition);

            Assert.AreEqual(EditionAuditCategory.Unverifiable, result);
        }

        [TestMethod]
        public async Task GetEnrichedOrdersAsync_EntryWithEditionMismatch_FlagsCategoryAndListsEditions()
        {
            SetUpPrinting(101, "LOB-EN001", "Ultra Rare");
            _collectionRepositoryMock.Setup(r => r.GetByStatusAsync(CollectionStatus.Ordered)).ReturnsAsync(
            [
                new CollectionEntry { ID = 1, CatalogPrintingID = 101, Edition = CardEdition.FirstEdition, Quantity = 1 }
            ]);
            SetUpEditions(1, (101, [CardEdition.Unlimited]));

            var result = (await _service.GetEnrichedOrdersAsync()).Single();

            Assert.AreEqual(EditionAuditCategory.EditionMismatch, result.Category);
            CollectionAssert.AreEqual(new[] { CardEdition.Unlimited }, result.AvailableEditions.ToArray());
        }

        [TestMethod]
        public async Task GetEnrichedOrdersAsync_EntryWithNoRecordedEdition_SkipsCategoryCheck()
        {
            SetUpPrinting(101, "LOB-EN001", "Ultra Rare");
            _collectionRepositoryMock.Setup(r => r.GetByStatusAsync(CollectionStatus.Ordered)).ReturnsAsync(
            [
                new CollectionEntry { ID = 1, CatalogPrintingID = 101, Edition = null, Quantity = 1 }
            ]);

            var result = (await _service.GetEnrichedOrdersAsync()).Single();

            Assert.IsNull(result.Category);
            Assert.AreEqual(0, result.AvailableEditions.Count);
        }

        [TestMethod]
        public async Task GetEnrichedOrdersAsync_OrdersFromSeveralCards_SortsByCardNameThenSetCodeThenNewest()
        {
            SetUpCard(1, "Dark Magician",
                new Set { Code = "SDK-EN001", PrintingID = 101, RarityName = "Ultra Rare" },
                new Set { Code = "LOB-EN005", PrintingID = 102, RarityName = "Ultra Rare" });
            SetUpCard(2, "Blue-Eyes White Dragon", new Set { Code = "SDK-EN001", PrintingID = 201, RarityName = "Ultra Rare" });
            _collectionRepositoryMock.Setup(r => r.GetByStatusAsync(CollectionStatus.Ordered)).ReturnsAsync(
            [
                new CollectionEntry { ID = 1, CatalogPrintingID = 101, DateCreated = new DateTime(2026, 10, 1) },
                new CollectionEntry { ID = 2, CatalogPrintingID = 102, DateCreated = new DateTime(2026, 10, 1) },
                new CollectionEntry { ID = 3, CatalogPrintingID = 201, DateCreated = new DateTime(2026, 10, 1) },
                new CollectionEntry { ID = 4, CatalogPrintingID = 102, DateCreated = new DateTime(2026, 10, 20) }
            ]);

            var result = await _service.GetEnrichedOrdersAsync();

            CollectionAssert.AreEqual(new[] { 3, 4, 2, 1 }, result.Select(o => o.EntryID).ToArray());
        }

        [TestMethod]
        public async Task GetEnrichedOrdersAsync_PrintingWithoutLinkedProduct_IsUnverifiable()
        {
            SetUpCard(1, "Synthetic Duelist",
                new Set { Code = "LDS1-EN045", PrintingID = 101, RarityName = "Ultra Rare" },
                new Set { Code = "LDS1-EN045", PrintingID = 102, PrintVariant = "Blue", RarityName = "Ultra Rare" });
            _collectionRepositoryMock.Setup(r => r.GetByStatusAsync(CollectionStatus.Ordered)).ReturnsAsync(
            [
                new CollectionEntry { ID = 1, CatalogPrintingID = 101, Edition = CardEdition.Unlimited, Quantity = 1 }
            ]);
            SetUpEditions(1, (102, [CardEdition.Unlimited]));

            var result = (await _service.GetEnrichedOrdersAsync()).Single();

            Assert.AreEqual(EditionAuditCategory.Unverifiable, result.Category);
        }

        [TestMethod]
        public async Task GetEnrichedOrdersAsync_VariantPrinting_UsesThatPrintingsEditions()
        {
            SetUpCard(1, "Synthetic Dragon",
                new Set { Code = "LOB-EN001", PrintingID = 101, RarityName = "Ultra Rare" },
                new Set { Code = "LOB-EN001", PrintingID = 102, PrintVariant = "Starfoil", RarityName = "Ultra Rare" });
            _collectionRepositoryMock.Setup(r => r.GetByStatusAsync(CollectionStatus.Ordered)).ReturnsAsync(
            [
                new CollectionEntry { ID = 1, CatalogPrintingID = 102, Edition = CardEdition.Unlimited, Quantity = 1 }
            ]);
            SetUpEditions(1, (101, [CardEdition.Unlimited]), (102, [CardEdition.FirstEdition]));

            var result = (await _service.GetEnrichedOrdersAsync()).Single();

            Assert.AreEqual(EditionAuditCategory.EditionMismatch, result.Category);
        }

        [TestMethod]
        public async Task SearchEditionAuditAsync_CategoryFilterDoesNotMatch_ExcludesResult()
        {
            SetUpEditionMismatchEntry();

            var result = await _service.SearchEditionAuditAsync(new EditionAuditSearchCriteria { Category = EditionAuditCategory.Unverifiable });

            Assert.AreEqual(0, result.TotalCount);
        }

        [TestMethod]
        public async Task SearchEditionAuditAsync_CategoryFilterMatches_ReturnsResult()
        {
            SetUpEditionMismatchEntry();

            var result = await _service.SearchEditionAuditAsync(new EditionAuditSearchCriteria { Category = EditionAuditCategory.EditionMismatch });

            Assert.AreEqual(1, result.TotalCount);
        }

        [TestMethod]
        public async Task SearchEditionAuditAsync_FlaggedEntry_AppearsInResults()
        {
            SetUpEditionMismatchEntry();

            var result = await _service.SearchEditionAuditAsync(new EditionAuditSearchCriteria());

            Assert.AreEqual(1, result.TotalCount);
            Assert.AreEqual(1, result.Items[0].FlaggedCount);
            Assert.AreEqual(EditionAuditCategory.EditionMismatch, result.Items[0].FlaggedCategory);
        }

        [TestMethod]
        public async Task SearchEditionAuditAsync_NoFlaggedEntries_ReturnsEmpty()
        {
            var result = await _service.SearchEditionAuditAsync(new EditionAuditSearchCriteria());

            Assert.AreEqual(0, result.TotalCount);
        }

        [TestMethod]
        public async Task SearchEditionAuditAsync_QueryFilterDoesNotMatch_ExcludesResult()
        {
            SetUpEditionMismatchEntry();

            var result = await _service.SearchEditionAuditAsync(new EditionAuditSearchCriteria { Query = "Blue-Eyes" });

            Assert.AreEqual(0, result.TotalCount);
        }

        [TestMethod]
        public async Task SearchEditionAuditAsync_QueryFilterMatchesCardName_ReturnsResult()
        {
            SetUpEditionMismatchEntry();

            var result = await _service.SearchEditionAuditAsync(new EditionAuditSearchCriteria { Query = "Dark" });

            Assert.AreEqual(1, result.TotalCount);
        }

        [TestMethod]
        public async Task SearchEditionAuditAsync_TwoPrintingsOfOneSetCodeAndRarity_ReturnsSeparateGroups()
        {
            SetUpCard(1, "Synthetic Dragon",
                new Set { Code = "LOB-EN001", PrintingID = 101, RarityName = "Ultra Rare" },
                new Set { Code = "LOB-EN001", PrintingID = 102, PrintVariant = "Starfoil", RarityName = "Ultra Rare" });
            var entries = new List<CollectionEntry>
            {
                new() { ID = 1, CatalogPrintingID = 101, Edition = CardEdition.FirstEdition, Quantity = 1, Status = CollectionStatus.Owned },
                new() { ID = 2, CatalogPrintingID = 102, Edition = CardEdition.FirstEdition, Quantity = 1, Status = CollectionStatus.Owned }
            };
            _collectionRepositoryMock.Setup(r => r.GetByStatusAsync(CollectionStatus.Owned)).ReturnsAsync(entries);
            _collectionRepositoryMock.Setup(r => r.GetByCardIDAsync(1)).ReturnsAsync(entries);
            SetUpEditions(1, (101, [CardEdition.Unlimited]), (102, [CardEdition.Unlimited]));

            var result = await _service.SearchEditionAuditAsync(new EditionAuditSearchCriteria());

            Assert.AreEqual(2, result.TotalCount);
        }

        private void SetUpEditionMismatchEntry()
        {
            SetUpPrinting(101, "LOB-EN001", "Ultra Rare");
            var entries = new List<CollectionEntry>
            {
                new() { ID = 1, CatalogPrintingID = 101, Edition = CardEdition.FirstEdition, Quantity = 1, Status = CollectionStatus.Owned }
            };
            _collectionRepositoryMock.Setup(r => r.GetByStatusAsync(CollectionStatus.Owned)).ReturnsAsync(entries);
            _collectionRepositoryMock.Setup(r => r.GetByCardIDAsync(1)).ReturnsAsync(entries);
            SetUpEditions(1, (101, [CardEdition.Unlimited]));
        }

        /// <summary>Makes the pricing mock list the given editions for each of the card's printings.</summary>
        private void SetUpEditions(int cardID, params (int PrintingID, CardEdition[] Editions)[] printings) =>
            _pricingServiceMock.Setup(p => p.GetEditionsByPrintingAsync(cardID)).ReturnsAsync(
                printings.ToDictionary(p => p.PrintingID, p => (IReadOnlySet<CardEdition>)p.Editions.ToHashSet()));
    }
}
