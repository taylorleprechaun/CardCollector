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
        public async Task SearchOrdersAsync_EntryWithEditionMismatch_FlagsCategoryAndListsEditions()
        {
            SetUpPrinting(101, "LOB-EN001", "Ultra Rare");
            _collectionRepositoryMock.Setup(r => r.GetByStatusAsync(CollectionStatus.Ordered)).ReturnsAsync(
            [
                new CollectionEntry { ID = 1, CatalogPrintingID = 101, Edition = CardEdition.FirstEdition, Quantity = 1 }
            ]);
            SetUpEditions(1, (101, [CardEdition.Unlimited]));

            var result = ((await _service.SearchOrdersAsync(new OrderSearchCriteria())).PagedItems.Items).Single();

            Assert.AreEqual(EditionAuditCategory.EditionMismatch, result.Category);
            CollectionAssert.AreEqual(new[] { CardEdition.Unlimited }, result.AvailableEditions.ToArray());
        }

        [TestMethod]
        public async Task SearchOrdersAsync_EntryWithNoRecordedEdition_SkipsCategoryCheck()
        {
            SetUpPrinting(101, "LOB-EN001", "Ultra Rare");
            _collectionRepositoryMock.Setup(r => r.GetByStatusAsync(CollectionStatus.Ordered)).ReturnsAsync(
            [
                new CollectionEntry { ID = 1, CatalogPrintingID = 101, Edition = null, Quantity = 1 }
            ]);

            var result = ((await _service.SearchOrdersAsync(new OrderSearchCriteria())).PagedItems.Items).Single();

            Assert.IsNull(result.Category);
            Assert.AreEqual(0, result.AvailableEditions.Count);
        }

        [TestMethod]
        public async Task SearchOrdersAsync_OrdersFromSeveralCards_SortsByCardNameThenSetCodeThenNewest()
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

            var result = (await _service.SearchOrdersAsync(new OrderSearchCriteria())).PagedItems.Items;

            CollectionAssert.AreEqual(new[] { 3, 4, 2, 1 }, result.Select(o => o.EntryID).ToArray());
        }

        [TestMethod]
        public async Task GetOrderedCardNameSuggestionsAsync_Query_SuggestsOnlyOrderedCardNamesOnce()
        {
            SetUpThreeOrderedCards();

            var result = await _service.GetOrderedCardNameSuggestionsAsync("i");

            CollectionAssert.AreEqual(new[] { "Blue-Eyes White Dragon", "Dark Magician", "Kuriboh" }, result.ToArray());
        }

        [TestMethod]
        public async Task GetOrderedCardNameSuggestionsAsync_BlankQuery_ReturnsEmpty()
        {
            var result = await _service.GetOrderedCardNameSuggestionsAsync("  ");

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public async Task SearchOrdersAsync_AnySearch_ListsSetAndRarityNamesAcrossEveryOrder()
        {
            SetUpThreeOrderedCards();

            var result = await _service.SearchOrdersAsync(new OrderSearchCriteria { Query = "kuriboh" });

            CollectionAssert.AreEqual(new[] { "Common", "Ultra Rare" }, result.AvailableRarityNames.ToArray());
            CollectionAssert.AreEqual(new[] { "Legend of Blue Eyes White Dragon", "Metal Raiders", "Starter Deck: Kaiba" }, result.AvailableSetNames.ToArray());
        }

        [TestMethod]
        public async Task SearchOrdersAsync_Query_KeepsOrdersMatchingCardNameOrSetCode()
        {
            SetUpThreeOrderedCards();

            var byName = await _service.SearchOrdersAsync(new OrderSearchCriteria { Query = "magician" });
            var bySetCode = await _service.SearchOrdersAsync(new OrderSearchCriteria { Query = "lob-" });

            CollectionAssert.AreEqual(new[] { 1 }, byName.PagedItems.Items.Select(o => o.EntryID).ToArray());
            CollectionAssert.AreEqual(new[] { 2 }, bySetCode.PagedItems.Items.Select(o => o.EntryID).ToArray());
        }

        [TestMethod]
        public async Task SearchOrdersAsync_RarityName_KeepsOrdersOfThatRarity()
        {
            SetUpThreeOrderedCards();

            var result = await _service.SearchOrdersAsync(new OrderSearchCriteria { RarityName = "Common" });

            CollectionAssert.AreEqual(new[] { 3 }, result.PagedItems.Items.Select(o => o.EntryID).ToArray());
        }

        [TestMethod]
        public async Task SearchOrdersAsync_SetName_KeepsOrdersFromThatSet()
        {
            SetUpThreeOrderedCards();
            _cardDataRepositoryMock.Setup(r => r.GetSetPrefixByName("Metal Raiders")).Returns("MRD");

            var result = await _service.SearchOrdersAsync(new OrderSearchCriteria { SetName = "Metal Raiders" });

            CollectionAssert.AreEqual(new[] { 3 }, result.PagedItems.Items.Select(o => o.EntryID).ToArray());
        }

        [TestMethod]
        public async Task SearchOrdersAsync_SecondPage_PagesItemsButCountsAndTotalsEveryMatch()
        {
            SetUpThreeOrderedCards();

            var result = await _service.SearchOrdersAsync(new OrderSearchCriteria { Page = 2, PageSize = 2 });

            CollectionAssert.AreEqual(new[] { 3 }, result.PagedItems.Items.Select(o => o.EntryID).ToArray());
            Assert.AreEqual(3, result.PagedItems.TotalCount);
            Assert.AreEqual(6, result.PagedItems.TotalQuantitySum);
            Assert.AreEqual(12.99m, result.TotalCost);
        }

        [TestMethod]
        public async Task SearchOrdersAsync_SecondPage_LooksUpEditionsOnlyForCardsOnThatPage()
        {
            SetUpThreeOrderedCards();

            await _service.SearchOrdersAsync(new OrderSearchCriteria { Page = 2, PageSize = 2 });

            _pricingServiceMock.Verify(p => p.GetEditionsByPrintingAsync(3), Times.Once);
            _pricingServiceMock.Verify(p => p.GetEditionsByPrintingAsync(1), Times.Never);
            _pricingServiceMock.Verify(p => p.GetEditionsByPrintingAsync(2), Times.Never);
        }

        [TestMethod]
        public async Task SearchOrdersAsync_SortDescending_SortsByCardNameDescending()
        {
            SetUpThreeOrderedCards();

            var result = await _service.SearchOrdersAsync(new OrderSearchCriteria { SortDescending = true });

            CollectionAssert.AreEqual(new[] { 3, 1, 2 }, result.PagedItems.Items.Select(o => o.EntryID).ToArray());
        }

        [TestMethod]
        public async Task SearchOrdersAsync_NullCriteria_ThrowsArgumentNullException()
        {
            await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => _service.SearchOrdersAsync(null!));
        }

        /// <summary>Orders for Dark Magician (1 × $2.50, entry 1), Blue-Eyes (2 × $4.00, entry 2), and Kuriboh (3 × $0.83, entry 3, Common).</summary>
        private void SetUpThreeOrderedCards()
        {
            SetUpCard(1, "Dark Magician", new Set { Code = "SDK-EN001", Name = "Starter Deck: Kaiba", PrintingID = 101, RarityName = "Ultra Rare" });
            SetUpCard(2, "Blue-Eyes White Dragon", new Set { Code = "LOB-EN001", Name = "Legend of Blue Eyes White Dragon", PrintingID = 201, RarityName = "Ultra Rare" });
            SetUpCard(3, "Kuriboh", new Set { Code = "MRD-EN071", Name = "Metal Raiders", PrintingID = 301, RarityName = "Common" });
            _collectionRepositoryMock.Setup(r => r.GetByStatusAsync(CollectionStatus.Ordered)).ReturnsAsync(
            [
                new CollectionEntry { ID = 1, CatalogPrintingID = 101, Edition = CardEdition.FirstEdition, PurchasePrice = 2.50m, Quantity = 1 },
                new CollectionEntry { ID = 2, CatalogPrintingID = 201, Edition = CardEdition.FirstEdition, PurchasePrice = 4.00m, Quantity = 2 },
                new CollectionEntry { ID = 3, CatalogPrintingID = 301, Edition = CardEdition.FirstEdition, PurchasePrice = 0.83m, Quantity = 3 }
            ]);
        }

        [TestMethod]
        public async Task SearchOrdersAsync_PrintingWithoutLinkedProduct_IsUnverifiable()
        {
            SetUpCard(1, "Synthetic Duelist",
                new Set { Code = "LDS1-EN045", PrintingID = 101, RarityName = "Ultra Rare" },
                new Set { Code = "LDS1-EN045", PrintingID = 102, PrintVariant = "Blue", RarityName = "Ultra Rare" });
            _collectionRepositoryMock.Setup(r => r.GetByStatusAsync(CollectionStatus.Ordered)).ReturnsAsync(
            [
                new CollectionEntry { ID = 1, CatalogPrintingID = 101, Edition = CardEdition.Unlimited, Quantity = 1 }
            ]);
            SetUpEditions(1, (102, [CardEdition.Unlimited]));

            var result = ((await _service.SearchOrdersAsync(new OrderSearchCriteria())).PagedItems.Items).Single();

            Assert.AreEqual(EditionAuditCategory.Unverifiable, result.Category);
        }

        [TestMethod]
        public async Task SearchOrdersAsync_VariantPrinting_UsesThatPrintingsEditions()
        {
            SetUpCard(1, "Synthetic Dragon",
                new Set { Code = "LOB-EN001", PrintingID = 101, RarityName = "Ultra Rare" },
                new Set { Code = "LOB-EN001", PrintingID = 102, PrintVariant = "Starfoil", RarityName = "Ultra Rare" });
            _collectionRepositoryMock.Setup(r => r.GetByStatusAsync(CollectionStatus.Ordered)).ReturnsAsync(
            [
                new CollectionEntry { ID = 1, CatalogPrintingID = 102, Edition = CardEdition.Unlimited, Quantity = 1 }
            ]);
            SetUpEditions(1, (101, [CardEdition.Unlimited]), (102, [CardEdition.FirstEdition]));

            var result = ((await _service.SearchOrdersAsync(new OrderSearchCriteria())).PagedItems.Items).Single();

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
