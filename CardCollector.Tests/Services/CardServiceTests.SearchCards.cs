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
