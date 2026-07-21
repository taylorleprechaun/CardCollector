using CardCollector.Data.Models;
using CardCollector.DTO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CardCollector.Tests.Services
{
    public partial class CardServiceTests
    {
        [TestMethod]
        public async Task AddEntryAsync_QuantityLessThanOne_ClampsToOne()
        {
            CollectionEntry? captured = null;
            _collectionRepositoryMock
                .Setup(r => r.AddAsync(It.IsAny<CollectionEntry>()))
                .Callback<CollectionEntry>(e => captured = e)
                .Returns(Task.CompletedTask);

            await _service.AddEntryAsync(1, 2, "LOB-EN001", CollectionStatus.Owned, 0, null, null, null, null, null);

            Assert.AreEqual(1, captured!.Quantity);
        }

        [TestMethod]
        public async Task AddEntryAsync_RarityNameIsWhitespace_StoresNullRarityName()
        {
            CollectionEntry? captured = null;
            _collectionRepositoryMock
                .Setup(r => r.AddAsync(It.IsAny<CollectionEntry>()))
                .Callback<CollectionEntry>(e => captured = e)
                .Returns(Task.CompletedTask);

            await _service.AddEntryAsync(1, 2, "LOB-EN001", CollectionStatus.Owned, 1, null, null, null, null, null, rarityName: "   ");

            Assert.IsNull(captured!.RarityName);
        }

        [TestMethod]
        public async Task AddEntryAsync_ValidQuantity_PassesThroughUnchanged()
        {
            CollectionEntry? captured = null;
            _collectionRepositoryMock
                .Setup(r => r.AddAsync(It.IsAny<CollectionEntry>()))
                .Callback<CollectionEntry>(e => captured = e)
                .Returns(Task.CompletedTask);

            await _service.AddEntryAsync(1, 2, "LOB-EN001", CollectionStatus.Owned, 2, null, null, null, null, null);

            Assert.AreEqual(2, captured!.Quantity);
        }

        [TestMethod]
        public async Task AddToCartAsync_QuantityAboveMax_ClampsToMaxCartQuantity()
        {
            PendingOrderLine? captured = null;
            _pendingOrderRepositoryMock
                .Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<PendingOrderLine>>()))
                .Callback<IEnumerable<PendingOrderLine>>(lines => captured = lines.Single())
                .Returns(Task.CompletedTask);
            _pendingOrderRepositoryMock.Setup(r => r.GetSummaryAsync()).ReturnsAsync((1, 10m));
            _pendingOrderRepositoryMock
                .Setup(r => r.GetStagedQuantitiesAsync())
                .ReturnsAsync(new Dictionary<(int, string, string), int>());

            await _service.AddToCartAsync(1, 2, "LOB-EN001", "Ultra Rare", 10, 5m);

            Assert.AreEqual(3, captured!.Quantity);
        }

        [TestMethod]
        public async Task AddToCartAsync_QuantityBelowMin_ClampsToOne()
        {
            PendingOrderLine? captured = null;
            _pendingOrderRepositoryMock
                .Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<PendingOrderLine>>()))
                .Callback<IEnumerable<PendingOrderLine>>(lines => captured = lines.Single())
                .Returns(Task.CompletedTask);
            _pendingOrderRepositoryMock.Setup(r => r.GetSummaryAsync()).ReturnsAsync((1, 10m));
            _pendingOrderRepositoryMock
                .Setup(r => r.GetStagedQuantitiesAsync())
                .ReturnsAsync(new Dictionary<(int, string, string), int>());

            await _service.AddToCartAsync(1, 2, "LOB-EN001", "Ultra Rare", 0, 5m);

            Assert.AreEqual(1, captured!.Quantity);
        }

        [TestMethod]
        public async Task AddToCartAsync_ReturnsSummaryAndMatchingCartQuantity()
        {
            _pendingOrderRepositoryMock
                .Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<PendingOrderLine>>()))
                .Returns(Task.CompletedTask);
            _pendingOrderRepositoryMock.Setup(r => r.GetSummaryAsync()).ReturnsAsync((4, 42.50m));
            _pendingOrderRepositoryMock
                .Setup(r => r.GetStagedQuantitiesAsync())
                .ReturnsAsync(new Dictionary<(int, string, string), int> { [(2, "LOB-EN001", "Ultra Rare")] = 3 });

            var (count, total, cartQuantity) = await _service.AddToCartAsync(1, 2, "LOB-EN001", "Ultra Rare", 2, 5m);

            Assert.AreEqual(4, count);
            Assert.AreEqual(42.50m, total);
            Assert.AreEqual(3, cartQuantity);
        }

        [TestMethod]
        [DataRow(null, DisplayName = "Null set code")]
        [DataRow("", DisplayName = "Empty set code")]
        [DataRow("   ", DisplayName = "Whitespace set code")]
        public async Task CheckInCardAsync_SetCodeIsBlank_ThrowsArgumentException(string? setCode)
        {
            await Assert.ThrowsExactlyAsync<ArgumentException>(() => _service.CheckInCardAsync(1, setCode!, "Ultra Rare"));
        }

        [TestMethod]
        public async Task CheckInCardAsync_ValidSetCode_RemovesCheckedOutRecord()
        {
            _checkedOutRepositoryMock.Setup(r => r.RemoveAsync(1, "LOB-EN001", "Ultra Rare")).ReturnsAsync(true);

            await _service.CheckInCardAsync(1, "LOB-EN001", "Ultra Rare");

            _checkedOutRepositoryMock.Verify(r => r.RemoveAsync(1, "LOB-EN001", "Ultra Rare"), Times.Once);
        }

        [TestMethod]
        public async Task CheckOutCardAsync_ExistingRecord_UpdatesQuantityInsteadOfCreating()
        {
            _checkedOutRepositoryMock
                .Setup(r => r.GetAsync(2, "LOB-EN001", "Ultra Rare"))
                .ReturnsAsync(new CheckedOutCard { CardID = 1, ImageID = 2, SetCode = "LOB-EN001", RarityName = "Ultra Rare", Quantity = 1 });

            await _service.CheckOutCardAsync(1, 2, "LOB-EN001", "Ultra Rare", 3);

            _checkedOutRepositoryMock.Verify(r => r.UpdateAsync(2, "LOB-EN001", "Ultra Rare", 3), Times.Once);
            _checkedOutRepositoryMock.Verify(r => r.AddAsync(It.IsAny<CheckedOutCard>()), Times.Never);
        }

        [TestMethod]
        public async Task CheckOutCardAsync_NoExistingRecord_CreatesNewRecord()
        {
            _checkedOutRepositoryMock.Setup(r => r.GetAsync(2, "LOB-EN001", "Ultra Rare")).ReturnsAsync((CheckedOutCard?)null);

            await _service.CheckOutCardAsync(1, 2, "LOB-EN001", "Ultra Rare", 3);

            _checkedOutRepositoryMock.Verify(r => r.AddAsync(It.Is<CheckedOutCard>(c => c.CardID == 1 && c.ImageID == 2 && c.Quantity == 3)), Times.Once);
            _checkedOutRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        }

        [TestMethod]
        [DataRow(null, DisplayName = "Null set code")]
        [DataRow("", DisplayName = "Empty set code")]
        public async Task CheckOutCardAsync_SetCodeIsBlank_ThrowsArgumentException(string? setCode)
        {
            await Assert.ThrowsExactlyAsync<ArgumentException>(() => _service.CheckOutCardAsync(1, 2, setCode!, "Ultra Rare", 1));
        }
        [TestMethod]
        public async Task DismissNewPrintingAsync_DelegatesToRepository()
        {
            await _service.DismissNewPrintingAsync(1, "LOB-EN001", "Ultra Rare");

            _dismissedNewPrintingRepositoryMock.Verify(r => r.AddAsync(1, "LOB-EN001", "Ultra Rare"), Times.Once);
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
            _collectionRepositoryMock.Setup(r => r.GetByStatusAsync(CollectionStatus.Owned)).ReturnsAsync(
            [
                new CollectionEntry { ID = 1, CardID = 1, ImageID = 2, SetCode = "LOB-EN001", Quantity = 1 }
            ]);

            var result = (await _service.GetEnrichedOwnedAsync()).ToList();

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(1, result[0].EntryID);
        }

        [TestMethod]
        public async Task GetEntriesByCardIDAsync_DelegatesToRepository()
        {
            var entries = new List<CollectionEntry> { new() { CardID = 1 } };
            _collectionRepositoryMock.Setup(r => r.GetByCardIDAsync(1)).ReturnsAsync(entries);

            var result = await _service.GetEntriesByCardIDAsync(1);

            CollectionAssert.AreEqual(entries, result.ToList());
        }

        [TestMethod]
        public async Task GetPendingCartAsync_ReturnsEnrichedLinesForEachPendingOrderLine()
        {
            _pendingOrderRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PendingOrderLine { ID = 1, CardID = 1, ImageID = 2, SetCode = "LOB-EN001", Quantity = 2 }
            ]);

            var result = await _service.GetPendingCartAsync();

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(1, result[0].PendingOrderLineID);
            Assert.AreEqual(2, result[0].Quantity);
        }

        [TestMethod]
        public async Task GetPreferredVersionByCardIDAsync_DelegatesToRepository()
        {
            var pv = new PreferredVersion { CardID = 1 };
            _preferredVersionRepositoryMock.Setup(r => r.GetByCardIDAsync(1)).ReturnsAsync(pv);

            var result = await _service.GetPreferredVersionByCardIDAsync(1);

            Assert.AreSame(pv, result);
        }

        [TestMethod]
        public async Task GetTrackedCardImageMapAsync_CardNameNotFoundInBrowseableCards_IsOmitted()
        {
            _collectionEntryValueRepositoryMock.Setup(r => r.GetDistinctCardNamesAsync()).ReturnsAsync(["Unknown Card"]);

            var map = await _service.GetTrackedCardImageMapAsync();

            Assert.AreEqual(0, map.Count);
        }

        [TestMethod]
        public async Task GetTrackedCardImageMapAsync_MapsCardNamesToSmallImageUrl()
        {
            _collectionEntryValueRepositoryMock.Setup(r => r.GetDistinctCardNamesAsync()).ReturnsAsync(["Dark Magician"]);
            _cardDataRepositoryMock.Setup(r => r.GetBrowseableCards()).Returns(
            [
                new Card { Name = "Dark Magician", CardImages = [new Image { ID = 1, ImageURLSmall = "small.jpg" }] }
            ]);

            var map = await _service.GetTrackedCardImageMapAsync();

            Assert.AreEqual("small.jpg", map["Dark Magician"]);
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
        public async Task SavePreferredVersionAsync_SavesAndClearsIgnoredStatus()
        {
            await _service.SavePreferredVersionAsync(1, 2, "LOB-EN001", "Ultra Rare");

            _preferredVersionRepositoryMock.Verify(r => r.AddOrUpdateAsync(1, 2, "LOB-EN001", "Ultra Rare"), Times.Once);
            _ignoredCardRepositoryMock.Verify(r => r.RemoveAsync(1), Times.Once);
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
        [TestMethod]
        public async Task UpgradePreferredVersionAsync_SavesNewVersionAndClearsIgnoredStatus()
        {
            await _service.UpgradePreferredVersionAsync(2, 1, "NEW-EN001", "Secret Rare");

            _preferredVersionRepositoryMock.Verify(r => r.AddOrUpdateAsync(1, 2, "NEW-EN001", "Secret Rare"), Times.Once);
            _ignoredCardRepositoryMock.Verify(r => r.RemoveAsync(1), Times.Once);
        }
    }
}
