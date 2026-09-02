using CardCollector.Data.Models;
using CardCollector.DTO;
using CardCollector.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CardCollector.Tests.Services
{
    public partial class CardServiceTests
    {
        [TestMethod]
        public async Task SearchCardsAsync_CardStatusIsOrdered_CompletionStatusIsNull()
        {
            SetUpBrowseableCards(new Card { ID = 1, Name = "Dark Magician", CardImages = [new Image { ID = 10 }] });
            _collectionRepositoryMock.Setup(r => r.GetStatusByCardIDsAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync(new Dictionary<int, CollectionStatus> { [1] = CollectionStatus.Ordered });

            var result = await _service.SearchCardsAsync(new BrowseSearchCriteria());

            Assert.AreEqual(CollectionStatus.Ordered, result.Items[0].Status);
            Assert.IsNull(result.Items[0].CompletionStatus);
        }

        [TestMethod]
        public async Task SearchCardsAsync_CardStatusIsOwned_PopulatesCompletionStatus()
        {
            SetUpBrowseableCards(new Card { ID = 1, Name = "Dark Magician", CardImages = [new Image { ID = 10 }] });
            _collectionRepositoryMock.Setup(r => r.GetStatusByCardIDsAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync(new Dictionary<int, CollectionStatus> { [1] = CollectionStatus.Owned });
            _collectionRepositoryMock.Setup(r => r.GetCompletionStatusByImageIDsAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync(new Dictionary<int, CollectionCompletionStatus> { [10] = CollectionCompletionStatus.Complete });

            var result = await _service.SearchCardsAsync(new BrowseSearchCriteria());

            Assert.AreEqual(CollectionStatus.Owned, result.Items[0].Status);
            Assert.AreEqual(CollectionCompletionStatus.Complete, result.Items[0].CompletionStatus);
        }

        [TestMethod]
        public async Task SearchCardsAsync_CardTypeFilter_MatchesSubstring()
        {
            SetUpBrowseableCards(
                new Card { ID = 1, Name = "Dark Magician", CardType = "Normal Monster" },
                new Card { ID = 2, Name = "Monster Reborn", CardType = "Spell Card" });

            var result = await _service.SearchCardsAsync(new BrowseSearchCriteria { CardType = "Monster" });

            Assert.AreEqual(1, result.TotalCount);
            Assert.AreEqual("Dark Magician", result.Items[0].Name);
        }

        [TestMethod]
        public async Task SearchCardsAsync_InCollectionFalse_ExcludesOwnedCardIDs()
        {
            SetUpBrowseableCards(
                new Card { ID = 1, Name = "Dark Magician" },
                new Card { ID = 2, Name = "Blue-Eyes White Dragon" });
            _collectionRepositoryMock.Setup(r => r.GetCardIDsByStatusAsync(CollectionStatus.Owned)).ReturnsAsync(new HashSet<int> { 1 });

            var result = await _service.SearchCardsAsync(new BrowseSearchCriteria { InCollection = false });

            Assert.AreEqual(1, result.TotalCount);
            Assert.AreEqual("Blue-Eyes White Dragon", result.Items[0].Name);
        }

        [TestMethod]
        public async Task SearchCardsAsync_InCollectionTrue_FiltersToOwnedCardIDs()
        {
            SetUpBrowseableCards(
                new Card { ID = 1, Name = "Dark Magician" },
                new Card { ID = 2, Name = "Blue-Eyes White Dragon" });
            _collectionRepositoryMock.Setup(r => r.GetCardIDsByStatusAsync(CollectionStatus.Owned)).ReturnsAsync(new HashSet<int> { 1 });

            var result = await _service.SearchCardsAsync(new BrowseSearchCriteria { InCollection = true });

            Assert.AreEqual(1, result.TotalCount);
            Assert.AreEqual("Dark Magician", result.Items[0].Name);
        }

        [TestMethod]
        public async Task SearchCardsAsync_InCollectionTrueWithSetFilter_ScopesToOwnedWithinThatSetNotWholeCard()
        {
            SetUpBrowseableCards(
                new Card { ID = 1, Name = "Dark Magician", CardSets = [new Set { Code = "LOB-EN001" }] },
                new Card { ID = 2, Name = "Blue-Eyes White Dragon", CardSets = [new Set { Code = "LOB-EN002" }] });
            _cardDataRepositoryMock.Setup(r => r.GetSetPrefixByName("Legend of Blue Eyes White Dragon")).Returns("LOB");
            // Both cards are owned somewhere in the collection, but only card 2 is owned specifically within LOB.
            _collectionRepositoryMock.Setup(r => r.GetCardIDsByStatusAsync(CollectionStatus.Owned)).ReturnsAsync(new HashSet<int> { 1, 2 });
            _collectionRepositoryMock
                .Setup(r => r.GetQuantitiesByCardIDsForPrintingAsync(It.IsAny<IEnumerable<int>>(), CollectionStatus.Owned, "LOB", null))
                .ReturnsAsync(new Dictionary<int, int> { [2] = 1 });

            var result = await _service.SearchCardsAsync(new BrowseSearchCriteria { SetName = "Legend of Blue Eyes White Dragon", InCollection = true });

            Assert.AreEqual(1, result.TotalCount);
            Assert.AreEqual("Blue-Eyes White Dragon", result.Items[0].Name);
        }

        [TestMethod]
        public async Task SearchCardsAsync_InWishlistTrue_ExcludesAlreadyCollectedCards()
        {
            SetUpBrowseableCards(
                new Card { ID = 1, Name = "Dark Magician" },
                new Card { ID = 2, Name = "Blue-Eyes White Dragon" });
            _collectionRepositoryMock.Setup(r => r.GetCardIDsByStatusAsync(CollectionStatus.Owned)).ReturnsAsync(new HashSet<int> { 2 });
            _collectionRepositoryMock.Setup(r => r.GetCardIDsByStatusAsync(CollectionStatus.Ordered)).ReturnsAsync(new HashSet<int>());
            _preferredVersionRepositoryMock.Setup(r => r.GetPreferredCardIDsAsync()).ReturnsAsync(new HashSet<int> { 1, 2 });

            var result = await _service.SearchCardsAsync(new BrowseSearchCriteria { InWishlist = true });

            Assert.AreEqual(1, result.TotalCount);
            Assert.AreEqual("Dark Magician", result.Items[0].Name);
        }

        [TestMethod]
        public async Task SearchCardsAsync_InWishlistTrueWithSetFilter_RequiresTrackedPrintingWithinThatSet()
        {
            SetUpBrowseableCards(
                new Card { ID = 1, Name = "A Bao A Qu", CardSets = [new Set { Code = "SUDA-EN001" }, new Set { Code = "RA04-EN001" }] });
            _cardDataRepositoryMock.Setup(r => r.GetSetPrefixByName("Quarter Century Bonanza")).Returns("RA04");
            // Card 1 is tracked, but only via a SUDA printing -- not the RA04 printing being filtered to.
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CardID = 1, ImageID = 10, SetCode = "SUDA-EN001", RarityName = "Secret Rare", DesiredQuantity = 3 }
            ]);
            _collectionRepositoryMock
                .Setup(r => r.GetQuantitiesByCardIDsForPrintingAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CollectionStatus>(), "RA04", null))
                .ReturnsAsync(new Dictionary<int, int>());

            var result = await _service.SearchCardsAsync(new BrowseSearchCriteria { SetName = "Quarter Century Bonanza", InWishlist = true });

            Assert.AreEqual(0, result.TotalCount);
        }

        [TestMethod]
        public async Task SearchCardsAsync_IsIncompleteTrueNoSetFilter_UsesCardWideCompletionMap()
        {
            SetUpBrowseableCards(
                new Card { ID = 1, Name = "Dark Magician", CardImages = [new Image { ID = 10 }] },
                new Card { ID = 2, Name = "Blue-Eyes White Dragon", CardImages = [new Image { ID = 20 }] });
            _collectionRepositoryMock.Setup(r => r.GetCompletionStatusByImageIDsAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync(new Dictionary<int, CollectionCompletionStatus> { [10] = CollectionCompletionStatus.Incomplete, [20] = CollectionCompletionStatus.Complete });

            var result = await _service.SearchCardsAsync(new BrowseSearchCriteria { IsIncomplete = true });

            Assert.AreEqual(1, result.TotalCount);
            Assert.AreEqual("Dark Magician", result.Items[0].Name);
        }

        [TestMethod]
        public async Task SearchCardsAsync_IsIncompleteWithRarityOnlyFilter_ScopesToThatRarityNotWholeCard()
        {
            SetUpBrowseableCards(
                new Card { ID = 1, Name = "Dark Magician", CardSets = [new Set { Code = "LOB-EN001", RarityName = "Ultra Rare" }] });
            _collectionRepositoryMock
                .Setup(r => r.GetQuantitiesByCardIDsForPrintingAsync(It.IsAny<IEnumerable<int>>(), CollectionStatus.Owned, null, "Ultra Rare"))
                .ReturnsAsync(new Dictionary<int, int> { [1] = 1 });
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = "Ultra Rare", DesiredQuantity = 3 }
            ]);

            var result = await _service.SearchCardsAsync(new BrowseSearchCriteria { RarityName = "Ultra Rare", IsIncomplete = true });

            Assert.AreEqual(1, result.TotalCount);
            Assert.AreEqual("Dark Magician", result.Items[0].Name);
        }

        [TestMethod]
        public async Task SearchCardsAsync_IsIncompleteWithSetFilter_UsesEachCardsOwnDesiredQuantityWithinThatSet()
        {
            SetUpBrowseableCards(
                new Card { ID = 1, Name = "Dark Magician", CardSets = [new Set { Code = "LOB-EN001" }] },
                new Card { ID = 2, Name = "Blue-Eyes White Dragon", CardSets = [new Set { Code = "LOB-EN002" }] });
            _cardDataRepositoryMock.Setup(r => r.GetSetPrefixByName("Legend of Blue Eyes White Dragon")).Returns("LOB");
            _collectionRepositoryMock
                .Setup(r => r.GetQuantitiesByCardIDsForPrintingAsync(It.IsAny<IEnumerable<int>>(), CollectionStatus.Owned, "LOB", null))
                .ReturnsAsync(new Dictionary<int, int> { [1] = 1, [2] = 1 });
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CardID = 1, ImageID = 10, SetCode = "LOB-EN001", DesiredQuantity = 1 },
                new PreferredVersion { CardID = 2, ImageID = 20, SetCode = "LOB-EN002", DesiredQuantity = 3 }
            ]);

            var result = await _service.SearchCardsAsync(new BrowseSearchCriteria { SetName = "Legend of Blue Eyes White Dragon", IsIncomplete = true });

            Assert.AreEqual(1, result.TotalCount);
            Assert.AreEqual("Blue-Eyes White Dragon", result.Items[0].Name);
        }

        [TestMethod]
        public async Task SearchCardsAsync_IsOrderedTrue_FiltersToOrderedCardIDs()
        {
            SetUpBrowseableCards(
                new Card { ID = 1, Name = "Dark Magician" },
                new Card { ID = 2, Name = "Blue-Eyes White Dragon" });
            _collectionRepositoryMock.Setup(r => r.GetCardIDsByStatusAsync(CollectionStatus.Ordered)).ReturnsAsync(new HashSet<int> { 2 });

            var result = await _service.SearchCardsAsync(new BrowseSearchCriteria { IsOrdered = true });

            Assert.AreEqual(1, result.TotalCount);
            Assert.AreEqual("Blue-Eyes White Dragon", result.Items[0].Name);
        }

        [TestMethod]
        public async Task SearchCardsAsync_IsTrackedFalseNoSetFilter_ExcludesTrackedCardIDs()
        {
            SetUpBrowseableCards(
                new Card { ID = 1, Name = "Dark Magician" },
                new Card { ID = 2, Name = "Blue-Eyes White Dragon" });
            _preferredVersionRepositoryMock.Setup(r => r.GetPreferredCardIDsAsync()).ReturnsAsync(new HashSet<int> { 1 });
            _collectionRepositoryMock.Setup(r => r.GetOwnedCardPrintingsAsync())
                .ReturnsAsync(new List<(int CardID, string SetCode, string? RarityName)>());

            var result = await _service.SearchCardsAsync(new BrowseSearchCriteria { IsTracked = false });

            Assert.AreEqual(1, result.TotalCount);
            Assert.AreEqual("Blue-Eyes White Dragon", result.Items[0].Name);
        }

        [TestMethod]
        public async Task SearchCardsAsync_IsTrackedFalseNoSetFilter_IncludesCardsTrackedElsewhereButOwnedAsAnUntrackedPrinting()
        {
            // "A Bao A Qu" is tracked as a SUDA Secret Rare, but the owned copy is an untracked RA03
            // Quarter Century Secret Rare -- the card-wide "tracked somewhere" check alone would miss this.
            SetUpBrowseableCards(new Card { ID = 1, Name = "A Bao A Qu" });
            _preferredVersionRepositoryMock.Setup(r => r.GetPreferredCardIDsAsync()).ReturnsAsync(new HashSet<int> { 1 });
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CardID = 1, ImageID = 10, SetCode = "SUDA-EN001", RarityName = "Secret Rare", DesiredQuantity = 3 }
            ]);
            _collectionRepositoryMock.Setup(r => r.GetOwnedCardPrintingsAsync()).ReturnsAsync(
                new List<(int CardID, string SetCode, string? RarityName)> { (1, "RA03-EN116", "Quarter Century Secret Rare") });

            var result = await _service.SearchCardsAsync(new BrowseSearchCriteria { IsTracked = false });

            Assert.AreEqual(1, result.TotalCount);
            Assert.AreEqual("A Bao A Qu", result.Items[0].Name);
        }

        [TestMethod]
        public async Task SearchCardsAsync_IsTrackedFalseWithSetFilter_FindsOwnedButUntrackedCardsWithinThatSet()
        {
            SetUpBrowseableCards(
                new Card { ID = 1, Name = "Dark Magician", CardSets = [new Set { Code = "LOB-EN001" }] },
                new Card { ID = 2, Name = "Blue-Eyes White Dragon", CardSets = [new Set { Code = "LOB-EN002" }] });
            _cardDataRepositoryMock.Setup(r => r.GetSetPrefixByName("Legend of Blue Eyes White Dragon")).Returns("LOB");
            _collectionRepositoryMock
                .Setup(r => r.GetQuantitiesByCardIDsForPrintingAsync(It.IsAny<IEnumerable<int>>(), CollectionStatus.Owned, "LOB", null))
                .ReturnsAsync(new Dictionary<int, int> { [1] = 3, [2] = 1 });
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CardID = 2, ImageID = 20, SetCode = "LOB-EN002", DesiredQuantity = 3 }
            ]);

            var result = await _service.SearchCardsAsync(new BrowseSearchCriteria
            {
                SetName = "Legend of Blue Eyes White Dragon",
                InCollection = true,
                IsTracked = false
            });

            Assert.AreEqual(1, result.TotalCount);
            Assert.AreEqual("Dark Magician", result.Items[0].Name);
        }

        [TestMethod]
        public async Task SearchCardsAsync_IsTrackedTrueNoSetFilter_ExcludesCardsWithAnUntrackedOwnedPrintingEvenIfTrackedElsewhere()
        {
            SetUpBrowseableCards(new Card { ID = 1, Name = "A Bao A Qu" });
            _preferredVersionRepositoryMock.Setup(r => r.GetPreferredCardIDsAsync()).ReturnsAsync(new HashSet<int> { 1 });
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CardID = 1, ImageID = 10, SetCode = "SUDA-EN001", RarityName = "Secret Rare", DesiredQuantity = 3 }
            ]);
            _collectionRepositoryMock.Setup(r => r.GetOwnedCardPrintingsAsync()).ReturnsAsync(
                new List<(int CardID, string SetCode, string? RarityName)> { (1, "RA03-EN116", "Quarter Century Secret Rare") });

            var result = await _service.SearchCardsAsync(new BrowseSearchCriteria { IsTracked = true });

            Assert.AreEqual(0, result.TotalCount);
        }
        [TestMethod]
        public async Task SearchCardsAsync_Pagination_SlicesItemsButKeepsTotalCountAtFullFilteredSize()
        {
            SetUpBrowseableCards(
                new Card { ID = 1, Name = "Card A" },
                new Card { ID = 2, Name = "Card B" },
                new Card { ID = 3, Name = "Card C" });

            var result = await _service.SearchCardsAsync(new BrowseSearchCriteria { Page = 2, PageSize = 2 });

            Assert.AreEqual(3, result.TotalCount);
            Assert.AreEqual(1, result.Items.Count);
            Assert.AreEqual("Card C", result.Items[0].Name);
        }

        [TestMethod]
        public async Task SearchCardsAsync_QueryFilter_MatchesCardNameCaseInsensitively()
        {
            SetUpBrowseableCards(
                new Card { ID = 1, Name = "Dark Magician" },
                new Card { ID = 2, Name = "Blue-Eyes White Dragon" });

            var result = await _service.SearchCardsAsync(new BrowseSearchCriteria { Query = "dark" });

            Assert.AreEqual(1, result.TotalCount);
            Assert.AreEqual("Dark Magician", result.Items[0].Name);
        }

        [TestMethod]
        public async Task SearchCardsAsync_RarityNameFilter_MatchesCardsWithThatRarity()
        {
            SetUpBrowseableCards(
                new Card { ID = 1, Name = "Dark Magician", CardSets = [new Set { Code = "LOB-EN001", RarityName = "Ultra Rare" }] },
                new Card { ID = 2, Name = "Blue-Eyes White Dragon", CardSets = [new Set { Code = "LOB-EN002", RarityName = "Common" }] });

            var result = await _service.SearchCardsAsync(new BrowseSearchCriteria { RarityName = "Ultra Rare" });

            Assert.AreEqual(1, result.TotalCount);
            Assert.AreEqual("Dark Magician", result.Items[0].Name);
        }

        [TestMethod]
        public async Task SearchCardsAsync_RarityNameFilterCommon_MatchesCardsWithRawShortPrintRarity()
        {
            SetUpBrowseableCards(
                new Card { ID = 1, Name = "Dark Magician", CardSets = [new Set { Code = "LOB-EN001", RarityName = "Short Print" }] },
                new Card { ID = 2, Name = "Blue-Eyes White Dragon", CardSets = [new Set { Code = "LOB-EN002", RarityName = "Ultra Rare" }] });

            var result = await _service.SearchCardsAsync(new BrowseSearchCriteria { RarityName = "Common" });

            Assert.AreEqual(1, result.TotalCount);
            Assert.AreEqual("Dark Magician", result.Items[0].Name);
        }

        [TestMethod]
        public async Task SearchCardsAsync_SetNameFilter_ResolvesPrefixAndMatchesCards()
        {
            SetUpBrowseableCards(
                new Card { ID = 1, Name = "Dark Magician", CardSets = [new Set { Code = "LOB-EN001" }] },
                new Card { ID = 2, Name = "Blue-Eyes White Dragon", CardSets = [new Set { Code = "SDK-EN001" }] });
            _cardDataRepositoryMock.Setup(r => r.GetSetPrefixByName("Legend of Blue Eyes White Dragon")).Returns("LOB");

            var result = await _service.SearchCardsAsync(new BrowseSearchCriteria { SetName = "Legend of Blue Eyes White Dragon" });

            Assert.AreEqual(1, result.TotalCount);
            Assert.AreEqual("Dark Magician", result.Items[0].Name);
        }
        private void SetUpBrowseableCards(params Card[] cards) =>
                                                                                                            _cardDataRepositoryMock.Setup(r => r.GetBrowseableCards()).Returns(cards);
    }
}
