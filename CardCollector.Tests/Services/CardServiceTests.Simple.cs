using CardCollector.Data.Models;
using CardCollector.DTO;
using CardCollector.Models.Catalog;
using CardCollector.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CardCollector.Tests.Services
{
    public partial class CardServiceTests
    {
        [TestMethod]
        public async Task AddEntryAsync_EnteredSetCodeHasNoKnownReleaseDate_DoesNotAutoDismissAnything()
        {
            SetUpCard(1, "Dark Magician",
                new Set { Code = "LOB-EN001", PrintingID = 101, RarityName = "Ultra Rare" },
                new Set { Code = "SDK-EN001", PrintingID = 102, RarityName = "Ultra Rare" });
            _cardSetRepositoryMock.Setup(r => r.GetTCGDateBySetCode("LOB-EN001")).Returns((string?)null);

            await _service.AddEntryAsync(101, CollectionStatus.Owned, 1, null, null, null, null, null);

            _dismissedNewPrintingRepositoryMock.Verify(r => r.AddAsync(It.IsAny<int>()), Times.Never);
        }

        [TestMethod]
        public async Task AddEntryAsync_NewerPrintingsAlreadyReleased_AreAutoDismissed_OlderAndFuturePrintingsAreNot()
        {
            SetUpCard(1, "Dark Magician",
                new Set { Code = "LOB-EN001", PrintingID = 101, RarityName = "Ultra Rare" },
                new Set { Code = "SDK-EN001", PrintingID = 102, RarityName = "Common" },
                new Set { Code = "FUT-EN001", PrintingID = 103, RarityName = "Secret Rare" },
                new Set { Code = "OLD-EN001", PrintingID = 104, RarityName = "Common" },
                new Set { Code = "UNK-EN001", PrintingID = 105, RarityName = "Common" });
            _cardSetRepositoryMock.Setup(r => r.GetTCGDateBySetCode("LOB-EN001")).Returns("2002-03-08");
            _cardSetRepositoryMock.Setup(r => r.GetTCGDateBySetCode("SDK-EN001")).Returns("2020-01-01");
            _cardSetRepositoryMock.Setup(r => r.GetTCGDateBySetCode("FUT-EN001")).Returns("2999-01-01");
            _cardSetRepositoryMock.Setup(r => r.GetTCGDateBySetCode("OLD-EN001")).Returns("1999-01-01");
            _cardSetRepositoryMock.Setup(r => r.GetTCGDateBySetCode("UNK-EN001")).Returns((string?)null);

            await _service.AddEntryAsync(101, CollectionStatus.Owned, 1, null, null, null, null, null);

            _dismissedNewPrintingRepositoryMock.Verify(r => r.AddAsync(102), Times.Once);
            _dismissedNewPrintingRepositoryMock.Verify(r => r.AddAsync(It.Is<int>(id => id != 102)), Times.Never);
        }

        [TestMethod]
        public async Task AddEntryAsync_NewerSetHasTwoPrintVariantsOfSameRarity_DismissesEachPrinting()
        {
            SetUpCard(1, "Dark Magician",
                new Set { Code = "LOB-EN001", PrintingID = 101, RarityName = "Ultra Rare" },
                new Set { Code = "RA05-EN001", PrintingID = 102, RarityName = "Ultra Rare", PrintVariant = "Extended Art" },
                new Set { Code = "RA05-EN001", PrintingID = 103, RarityName = "Ultra Rare", PrintVariant = "Starlight Rare" });
            _cardSetRepositoryMock.Setup(r => r.GetTCGDateBySetCode("LOB-EN001")).Returns("2002-03-08");
            _cardSetRepositoryMock.Setup(r => r.GetTCGDateBySetCode("RA05-EN001")).Returns("2020-01-01");

            await _service.AddEntryAsync(101, CollectionStatus.Owned, 1, null, null, null, null, null);

            _dismissedNewPrintingRepositoryMock.Verify(r => r.AddAsync(102), Times.Once);
            _dismissedNewPrintingRepositoryMock.Verify(r => r.AddAsync(103), Times.Once);
        }

        [TestMethod]
        public async Task AddEntryAsync_Printing_AddsEntryOnThatPrinting()
        {
            SetUpPrinting(101, "LOB-EN001", "Ultra Rare");

            await _service.AddEntryAsync(101, CollectionStatus.Owned, 2, CardCondition.NearMint, CardEdition.FirstEdition, null, null, null);

            _collectionRepositoryMock.Verify(r => r.AddAsync(It.Is<CollectionEntry>(e =>
                e.CatalogPrintingID == 101 && e.Quantity == 2 && e.Status == CollectionStatus.Owned)), Times.Once);
        }

        [TestMethod]
        [DataRow(0, 1, DisplayName = "Below one clamps to one")]
        [DataRow(2, 2, DisplayName = "Valid quantity passes through")]
        public async Task AddEntryAsync_Quantity_IsAtLeastOne(int requested, int expected)
        {
            SetUpPrinting(101, "LOB-EN001", "Ultra Rare");
            CollectionEntry? captured = null;
            _collectionRepositoryMock
                .Setup(r => r.AddAsync(It.IsAny<CollectionEntry>()))
                .Callback<CollectionEntry>(e => captured = e)
                .Returns(Task.CompletedTask);

            await _service.AddEntryAsync(101, CollectionStatus.Owned, requested, null, null, null, null, null);

            Assert.AreEqual(expected, captured!.Quantity);
        }

        [TestMethod]
        public async Task AddEntryAsync_UnknownPrinting_ThrowsArgumentException()
        {
            await Assert.ThrowsExactlyAsync<ArgumentException>(() =>
                _service.AddEntryAsync(999, CollectionStatus.Owned, 1, null, null, null, null, null));
        }

        [TestMethod]
        [DataRow(10, 3, DisplayName = "Above the cart maximum")]
        [DataRow(0, 1, DisplayName = "Below one")]
        public async Task AddToCartAsync_Quantity_IsClampedToCartRange(int requested, int expected)
        {
            SetUpPrinting(101, "LOB-EN001", "Ultra Rare");
            PendingOrderLine? captured = null;
            _pendingOrderRepositoryMock
                .Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<PendingOrderLine>>()))
                .Callback<IEnumerable<PendingOrderLine>>(lines => captured = lines.Single())
                .Returns(Task.CompletedTask);
            _pendingOrderRepositoryMock.Setup(r => r.GetSummaryAsync()).ReturnsAsync((1, 10m));

            await _service.AddToCartAsync(101, requested, 5m);

            Assert.AreEqual(expected, captured!.Quantity);
            Assert.AreEqual(101, captured.CatalogPrintingID);
        }

        [TestMethod]
        public async Task AddToCartAsync_ReturnsSummaryAndThePrintingsCartQuantity()
        {
            SetUpPrinting(101, "LOB-EN001", "Ultra Rare");
            _pendingOrderRepositoryMock.Setup(r => r.GetSummaryAsync()).ReturnsAsync((4, 42.50m));
            _pendingOrderRepositoryMock
                .Setup(r => r.GetStagedQuantitiesAsync())
                .ReturnsAsync(new Dictionary<int, int> { [101] = 3, [102] = 1 });

            var (count, total, cartQuantity) = await _service.AddToCartAsync(101, 2, 5m);

            Assert.AreEqual(4, count);
            Assert.AreEqual(42.50m, total);
            Assert.AreEqual(3, cartQuantity);
        }

        [TestMethod]
        public async Task AddToCartAsync_UnknownPrinting_ThrowsArgumentException()
        {
            await Assert.ThrowsExactlyAsync<ArgumentException>(() => _service.AddToCartAsync(999, 1, null));
        }

        [TestMethod]
        public async Task CheckEntryEditionAsync_EditionListedForPrinting_ReturnsNull()
        {
            SetUpPrinting(101, "LOB-EN001", "Ultra Rare");
            _pricingServiceMock.Setup(p => p.GetEditionsByPrintingAsync(1))
                .ReturnsAsync(new Dictionary<int, IReadOnlySet<CardEdition>> { [101] = new HashSet<CardEdition> { CardEdition.FirstEdition } });

            var result = await _service.CheckEntryEditionAsync(101, CardEdition.FirstEdition);

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task CheckEntryEditionAsync_OnlyOtherEditionsListed_ReturnsMismatch()
        {
            SetUpPrinting(101, "LOB-EN001", "Ultra Rare");
            _pricingServiceMock.Setup(p => p.GetEditionsByPrintingAsync(1))
                .ReturnsAsync(new Dictionary<int, IReadOnlySet<CardEdition>> { [101] = new HashSet<CardEdition> { CardEdition.Unlimited } });

            var result = await _service.CheckEntryEditionAsync(101, CardEdition.FirstEdition);

            Assert.AreEqual(EditionAuditCategory.EditionMismatch, result);
        }

        [TestMethod]
        public async Task CheckEntryEditionAsync_PrintingWithoutProduct_ReturnsUnverifiable()
        {
            SetUpPrinting(101, "LOB-EN001", "Ultra Rare");
            _pricingServiceMock.Setup(p => p.GetEditionsByPrintingAsync(1))
                .ReturnsAsync(new Dictionary<int, IReadOnlySet<CardEdition>>());

            var result = await _service.CheckEntryEditionAsync(101, CardEdition.FirstEdition);

            Assert.AreEqual(EditionAuditCategory.Unverifiable, result);
        }

        [TestMethod]
        public async Task CheckInCardAsync_Printing_RemovesItsCheckedOutRecord()
        {
            _checkedOutRepositoryMock.Setup(r => r.RemoveAsync(101)).ReturnsAsync(true);

            await _service.CheckInCardAsync(101);

            _checkedOutRepositoryMock.Verify(r => r.RemoveAsync(101), Times.Once);
        }

        [TestMethod]
        public async Task CheckOutCardAsync_ExistingRecord_UpdatesQuantityInsteadOfCreating()
        {
            _checkedOutRepositoryMock
                .Setup(r => r.GetAsync(101))
                .ReturnsAsync(new CheckedOutCard { CatalogPrintingID = 101, Quantity = 1 });

            await _service.CheckOutCardAsync(101, 3);

            _checkedOutRepositoryMock.Verify(r => r.UpdateAsync(101, 3), Times.Once);
            _checkedOutRepositoryMock.Verify(r => r.AddAsync(It.IsAny<CheckedOutCard>()), Times.Never);
        }

        [TestMethod]
        public async Task CheckOutCardAsync_NoExistingRecord_CreatesRecordOnThePrinting()
        {
            SetUpPrinting(101, "LOB-EN001", "Ultra Rare");
            _checkedOutRepositoryMock.Setup(r => r.GetAsync(101)).ReturnsAsync((CheckedOutCard?)null);

            await _service.CheckOutCardAsync(101, 3);

            _checkedOutRepositoryMock.Verify(r => r.AddAsync(It.Is<CheckedOutCard>(c => c.CatalogPrintingID == 101 && c.Quantity == 3)), Times.Once);
        }

        [TestMethod]
        public async Task CheckOutCardAsync_UnknownPrinting_ThrowsArgumentException()
        {
            await Assert.ThrowsExactlyAsync<ArgumentException>(() => _service.CheckOutCardAsync(999, 1));
        }

        [TestMethod]
        public async Task DismissNewPrintingAsync_Printing_DismissesIt()
        {
            SetUpPrinting(101, "RA05-EN001", "Ultra Rare", "Starlight Rare");

            await _service.DismissNewPrintingAsync(101);

            _dismissedNewPrintingRepositoryMock.Verify(r => r.AddAsync(101), Times.Once);
        }

        [TestMethod]
        public void GetCardByID_DelegatesToCardDataRepository()
        {
            var card = new Card { ID = 5 };
            _cardDataRepositoryMock.Setup(r => r.GetCardByID(5)).Returns(card);

            var result = _service.GetCardByID(5);

            Assert.AreSame(card, result);
        }

        [TestMethod]
        public void GetCardNameSuggestions_FiltersOrdersAndLimitsResults()
        {
            _cardDataRepositoryMock.Setup(r => r.GetBrowseableCards()).Returns(
            [
                new Card { Name = "Dark Magician" },
                new Card { Name = "Dark Magician Girl" },
                new Card { Name = "Blue-Eyes White Dragon" }
            ]);

            var result = _service.GetCardNameSuggestions("dark", maxResults: 10).ToList();

            CollectionAssert.AreEqual(new[] { "Dark Magician", "Dark Magician Girl" }, result);
        }

        [TestMethod]
        public void GetCardNameSuggestions_MaxResultsLimitsCount()
        {
            _cardDataRepositoryMock.Setup(r => r.GetBrowseableCards()).Returns(
            [
                new Card { Name = "Dark Magician" },
                new Card { Name = "Dark Magician Girl" }
            ]);

            var result = _service.GetCardNameSuggestions("dark", maxResults: 1).ToList();

            Assert.AreEqual(1, result.Count);
        }

        [TestMethod]
        public async Task GetCartSummaryAsync_DelegatesToRepository()
        {
            _pendingOrderRepositoryMock.Setup(r => r.GetSummaryAsync()).ReturnsAsync((2, 20m));

            var (count, total) = await _service.GetCartSummaryAsync();

            Assert.AreEqual(2, count);
            Assert.AreEqual(20m, total);
        }

        [TestMethod]
        public async Task GetEnrichedOwnedAsync_ReturnsEnrichedEntriesForOwnedStatus()
        {
            SetUpPrinting(101, "LOB-EN001", "Ultra Rare");
            _collectionRepositoryMock.Setup(r => r.GetByStatusAsync(CollectionStatus.Owned)).ReturnsAsync(
            [
                new CollectionEntry { ID = 1, CatalogPrintingID = 101, Quantity = 1 }
            ]);

            var result = (await _service.GetEnrichedOwnedAsync()).ToList();

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(1, result[0].EntryID);
        }

        [TestMethod]
        public async Task GetEnrichedOwnedAsync_EntryOnARetiredPrinting_ShowsItsCardWithANoteInsteadOfThePrinting()
        {
            _cardDataRepositoryMock.Setup(r => r.GetCardIDByPrintingID(150)).Returns(1);
            _cardDataRepositoryMock.Setup(r => r.GetCardByID(1)).Returns(new Card { ID = 1, Name = "Dark Magician" });
            _collectionRepositoryMock.Setup(r => r.GetByStatusAsync(CollectionStatus.Owned)).ReturnsAsync(
            [
                new CollectionEntry { ID = 1, CatalogPrintingID = 150, Quantity = 1 }
            ]);

            var entry = (await _service.GetEnrichedOwnedAsync()).Single();

            Assert.AreEqual(1, entry.CardID);
            Assert.AreEqual("Dark Magician", entry.CardName);
            Assert.AreEqual(150, entry.PrintingID);
            Assert.AreEqual("Printing no longer in the catalog", entry.SetName);
        }

        [TestMethod]
        public async Task GetEntriesByCardIDAsync_DelegatesToRepository()
        {
            var entries = new List<CollectionEntry> { new() { CatalogPrintingID = 101 } };
            _collectionRepositoryMock.Setup(r => r.GetByCardIDAsync(1)).ReturnsAsync(entries);

            var result = await _service.GetEntriesByCardIDAsync(1);

            CollectionAssert.AreEqual(entries, result.ToList());
        }

        [TestMethod]
        public async Task GetOwnedSetCodesAndRarityNamesAsync_OwnedPrintings_ReturnsSortedDistinctValuesAndSkipsUnknownPrintings()
        {
            SetUpCard(1, "Dark Magician",
                new Set { Code = "SDK-EN001", PrintingID = 101, RarityName = "Ultra Rare" },
                new Set { Code = "LOB-EN001", PrintingID = 102, RarityName = "Short Print" },
                new Set { Code = "LOB-EN001", PrintingID = 103, RarityName = "Ultra Rare", PrintVariant = "Starfoil" });
            _collectionRepositoryMock.Setup(r => r.GetOwnedPrintingIDsAsync()).ReturnsAsync(new HashSet<int> { 101, 102, 103, 999 });

            var (setCodes, rarityNames) = await _service.GetOwnedSetCodesAndRarityNamesAsync();

            CollectionAssert.AreEqual(new[] { "LOB-EN001", "SDK-EN001" }, setCodes.ToArray());
            CollectionAssert.AreEqual(new[] { "Common", "Ultra Rare" }, rarityNames.ToArray());
        }

        [TestMethod]
        public async Task GetPendingCartAsync_ReturnsEnrichedLinesForEachPendingOrderLine()
        {
            SetUpPrinting(101, "LOB-EN001", "Ultra Rare");
            _pendingOrderRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PendingOrderLine { ID = 1, CatalogPrintingID = 101, Quantity = 2 }
            ]);

            var result = await _service.GetPendingCartAsync();

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(1, result[0].PendingOrderLineID);
            Assert.AreEqual(2, result[0].Quantity);
        }

        [TestMethod]
        public async Task GetPriceHistoryCardsAsync_CardsWithHistory_ReturnsCurrentNamesSortedWithSmallImages()
        {
            _collectionEntryValueRepositoryMock.Setup(r => r.GetCatalogCardIDsAsync()).ReturnsAsync(new HashSet<int> { 1, 2 });
            SetUpCard(1, "Theorealize").CardImages = [new Image { ID = 10, ImageURL = "large.jpg", ImageURLSmall = "small.jpg" }];
            SetUpCard(2, "Dark Magician");

            var cards = await _service.GetPriceHistoryCardsAsync();

            CollectionAssert.AreEqual(new[] { "Dark Magician", "Theorealize" }, cards.Select(c => c.Name).ToArray());
            Assert.AreEqual("small.jpg", cards[1].ImageURL);
            Assert.IsNull(cards[0].ImageURL);
        }

        [TestMethod]
        public async Task GetPriceHistoryCardsAsync_CardNoLongerInTheCatalog_IsLeftOut()
        {
            _collectionEntryValueRepositoryMock.Setup(r => r.GetCatalogCardIDsAsync()).ReturnsAsync(new HashSet<int> { 999 });

            var cards = await _service.GetPriceHistoryCardsAsync();

            Assert.AreEqual(0, cards.Count);
        }

        [TestMethod]
        public async Task GetRandomUncollectedAsync_CardHasPreferredVersion_IsExcluded()
        {
            _cardDataRepositoryMock.Setup(r => r.GetBrowseableCards()).Returns(
            [
                new Card { ID = 1, Name = "Dark Magician" },
                new Card { ID = 2, Name = "Blue-Eyes White Dragon" }
            ]);
            _preferredVersionRepositoryMock.Setup(r => r.GetPreferredCardIDsAsync()).ReturnsAsync(new HashSet<int> { 1 });

            var result = await _service.GetRandomUncollectedAsync();

            Assert.AreEqual(2, result!.ID);
        }

        [TestMethod]
        public async Task GetRandomUncollectedAsync_IgnoredCardWithNoResurfacingOpportunity_IsExcluded()
        {
            _cardDataRepositoryMock.Setup(r => r.GetBrowseableCards()).Returns(
            [
                new Card { ID = 1, Name = "Dark Magician" },
                new Card { ID = 2, Name = "Blue-Eyes White Dragon" }
            ]);
            _ignoredCardRepositoryMock.Setup(r => r.GetIgnoredCardIDsAsync()).ReturnsAsync(new HashSet<int> { 1 });

            var result = await _service.GetRandomUncollectedAsync();

            Assert.AreEqual(2, result!.ID);
        }

        [TestMethod]
        public async Task GetRandomUncollectedAsync_IgnoredCardWithResurfacingOpportunity_IsNotExcluded()
        {
            _cardDataRepositoryMock.Setup(r => r.GetBrowseableCards()).Returns(
            [
                new Card { ID = 1, Name = "Dark Magician" }
            ]);
            SetUpCard(1, "Dark Magician", new Set { Code = "SDK-EN001", PrintingID = 101, RarityName = "Ultra Rare" });
            _cardSetRepositoryMock.Setup(r => r.GetTCGDateBySetCode("SDK-EN001")).Returns("2020-01-01");
            _ignoredCardRepositoryMock.Setup(r => r.GetIgnoredCardIDsAsync()).ReturnsAsync(new HashSet<int> { 1 });
            _ignoredCardRepositoryMock.Setup(r => r.GetAllAsync())
                .ReturnsAsync(new Dictionary<int, DateTime> { [1] = new DateTime(2015, 1, 1) });

            var result = await _service.GetRandomUncollectedAsync();

            Assert.AreEqual(1, result!.ID);
        }

        [TestMethod]
        public async Task GetRandomUncollectedAsync_NoBrowseableCards_ReturnsNull()
        {
            var result = await _service.GetRandomUncollectedAsync();

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetRandomUncollectedAsync_SingleUncollectedCard_ReturnsIt()
        {
            _cardDataRepositoryMock.Setup(r => r.GetBrowseableCards()).Returns([new Card { ID = 5, Name = "Dark Magician" }]);

            var result = await _service.GetRandomUncollectedAsync();

            Assert.AreEqual(5, result!.ID);
        }

        [TestMethod]
        public async Task GetTrackedPrintingsByCardIDAsync_DelegatesToRepository()
        {
            var trackedPrintings = (IReadOnlyList<PreferredVersion>)[new PreferredVersion { }];
            _preferredVersionRepositoryMock.Setup(r => r.GetByCardIDAsync(1)).ReturnsAsync(trackedPrintings);

            var result = await _service.GetTrackedPrintingsByCardIDAsync(1);

            Assert.AreSame(trackedPrintings, result);
        }

        [TestMethod]
        public async Task SavePreferredVersionAsync_Printing_TracksItAndClearsIgnoredStatus()
        {
            SetUpPrinting(101, "LOB-EN001", "Ultra Rare");

            await _service.SavePreferredVersionAsync(101);

            _preferredVersionRepositoryMock.Verify(r => r.AddOrUpdateAsync(101, null), Times.Once);
            _ignoredCardRepositoryMock.Verify(r => r.RemoveAsync(1), Times.Once);
        }

        [TestMethod]
        public async Task SavePreferredVersionAsync_UnknownPrinting_ThrowsArgumentException()
        {
            await Assert.ThrowsExactlyAsync<ArgumentException>(() => _service.SavePreferredVersionAsync(999));
        }

        [TestMethod]
        public async Task UpgradePreferredVersionAsync_MovesTrackedPrintingAndClearsIgnoredStatus()
        {
            SetUpPrinting(102, "NEW-EN001", "Secret Rare");

            await _service.UpgradePreferredVersionAsync(2, 102);

            _preferredVersionRepositoryMock.Verify(r => r.UpgradeAsync(2, 102), Times.Once);
            _ignoredCardRepositoryMock.Verify(r => r.RemoveAsync(1), Times.Once);
        }
        [TestMethod]
        public async Task IgnoreCardAsync_DelegatesToRepository()
        {
            await _service.IgnoreCardAsync(1);

            _ignoredCardRepositoryMock.Verify(r => r.AddAsync(1), Times.Once);
        }

        [TestMethod]
        public async Task IsCardIgnoredAsync_DelegatesToRepository()
        {
            _ignoredCardRepositoryMock.Setup(r => r.IsIgnoredAsync(1)).ReturnsAsync(true);

            var result = await _service.IsCardIgnoredAsync(1);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public async Task RemoveFromWishlistAsync_DelegatesToRepository()
        {
            await _service.RemoveFromWishlistAsync(5);

            _preferredVersionRepositoryMock.Verify(r => r.DeleteAsync(5), Times.Once);
        }

        [TestMethod]
        public async Task UnignoreCardAsync_DelegatesToRepository()
        {
            await _service.UnignoreCardAsync(1);

            _ignoredCardRepositoryMock.Verify(r => r.RemoveAsync(1), Times.Once);
        }

        [TestMethod]
        public async Task UpdateCartLineQuantityAsync_NoSuchLine_ReturnsFalse()
        {
            _pendingOrderRepositoryMock.Setup(r => r.UpdateQuantityAsync(It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(false);

            var result = await _service.UpdateCartLineQuantityAsync(999, 1);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task UpdateCartLineQuantityAsync_QuantityAboveMax_ClampsBeforeCallingRepository()
        {
            _pendingOrderRepositoryMock.Setup(r => r.UpdateQuantityAsync(1, 3)).ReturnsAsync(true);

            var result = await _service.UpdateCartLineQuantityAsync(1, 10);

            Assert.IsTrue(result);
            _pendingOrderRepositoryMock.Verify(r => r.UpdateQuantityAsync(1, 3), Times.Once);
        }
    }
}
