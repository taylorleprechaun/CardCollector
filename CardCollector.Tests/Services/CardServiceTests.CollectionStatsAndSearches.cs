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
        public async Task GetCollectionStatsAsync_BuildsRaritySetAndAcquisitionBreakdowns()
        {
            _collectionRepositoryMock.Setup(r => r.GetByStatusAsync(CollectionStatus.Owned)).ReturnsAsync(
            [
                new CollectionEntry { ID = 1, CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = "Ultra Rare", AcquisitionMethod = AcquisitionMethod.Purchased, Quantity = 2 },
                new CollectionEntry { ID = 2, CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = null, AcquisitionMethod = null, Quantity = 1 }
            ]);
            _cardDataRepositoryMock.Setup(r => r.GetSetNamesByCode())
                .Returns(new Dictionary<string, string> { ["LOB-EN001"] = "Legend of Blue Eyes White Dragon" });

            var stats = await _service.GetCollectionStatsAsync();

            Assert.IsTrue(stats.RarityBreakdown.Any(x => x.Label == "Ultra Rare" && x.Count == 1));
            Assert.IsTrue(stats.RarityBreakdown.Any(x => x.Label == "Unknown" && x.Count == 1));
            Assert.IsTrue(stats.AcquisitionBreakdown.Any(x => x.Label == "Purchased" && x.Count == 2));
            Assert.IsTrue(stats.AcquisitionBreakdown.Any(x => x.Label == "Unknown" && x.Count == 1));
            Assert.IsTrue(stats.SetBreakdown.Any(x => x.Label == "Legend of Blue Eyes White Dragon"));
        }

        [TestMethod]
        public async Task SearchCheckedOutAsync_CardTypeFilter_ExcludesNonMatchingType()
        {
            _cardDataRepositoryMock.Setup(r => r.GetCardByID(1)).Returns(new Card { ID = 1, Name = "Dark Magician", CardType = "Normal Monster" });
            _checkedOutRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new CheckedOutCard { ID = 1, CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = "Ultra Rare", Quantity = 1 }
            ]);

            var result = await _service.SearchCheckedOutAsync(new CheckedOutSearchCriteria { CardType = "Spell" });

            Assert.AreEqual(0, result.TotalCount);
        }

        [TestMethod]
        public async Task SearchCheckedOutAsync_QueryDoesNotMatch_ExcludesResult()
        {
            _cardDataRepositoryMock.Setup(r => r.GetCardByID(1)).Returns(new Card { ID = 1, Name = "Dark Magician" });
            _checkedOutRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new CheckedOutCard { ID = 1, CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = "Ultra Rare", Quantity = 1 }
            ]);

            var result = await _service.SearchCheckedOutAsync(new CheckedOutSearchCriteria { Query = "Blue-Eyes" });

            Assert.AreEqual(0, result.TotalCount);
        }

        [TestMethod]
        public async Task SearchCheckedOutAsync_QueryFilter_MatchesCardName()
        {
            _cardDataRepositoryMock.Setup(r => r.GetCardByID(1)).Returns(new Card { ID = 1, Name = "Dark Magician" });
            _checkedOutRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new CheckedOutCard { ID = 1, CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = "Ultra Rare", Quantity = 2 }
            ]);

            var result = await _service.SearchCheckedOutAsync(new CheckedOutSearchCriteria { Query = "Dark" });

            Assert.AreEqual(1, result.TotalCount);
            Assert.AreEqual(2, result.TotalQuantitySum);
        }

        [TestMethod]
        public async Task SearchCheckedOutAsync_RarityNameFilter_ExcludesNonMatchingRarity()
        {
            _cardDataRepositoryMock.Setup(r => r.GetCardByID(1)).Returns(new Card { ID = 1, Name = "Dark Magician" });
            _checkedOutRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new CheckedOutCard { ID = 1, CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = "Ultra Rare", Quantity = 1 }
            ]);

            var result = await _service.SearchCheckedOutAsync(new CheckedOutSearchCriteria { RarityName = "Common" });

            Assert.AreEqual(0, result.TotalCount);
        }

        [TestMethod]
        public async Task SearchCheckedOutAsync_SetNameFilter_ExcludesNonMatchingSetPrefix()
        {
            _cardDataRepositoryMock.Setup(r => r.GetCardByID(1)).Returns(new Card { ID = 1, Name = "Dark Magician" });
            _cardDataRepositoryMock.Setup(r => r.GetSetPrefixByName("Metal Raiders")).Returns("MRD");
            _checkedOutRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new CheckedOutCard { ID = 1, CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = "Ultra Rare", Quantity = 1 }
            ]);

            var result = await _service.SearchCheckedOutAsync(new CheckedOutSearchCriteria { SetName = "Metal Raiders" });

            Assert.AreEqual(0, result.TotalCount);
        }

        [TestMethod]
        public async Task SearchCheckedOutAsync_SetNameFilter_UnknownSetName_DoesNotFilter()
        {
            _cardDataRepositoryMock.Setup(r => r.GetCardByID(1)).Returns(new Card { ID = 1, Name = "Dark Magician" });
            _cardDataRepositoryMock.Setup(r => r.GetSetPrefixByName("Not A Real Set")).Returns((string?)null);
            _checkedOutRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new CheckedOutCard { ID = 1, CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = "Ultra Rare", Quantity = 1 }
            ]);

            var result = await _service.SearchCheckedOutAsync(new CheckedOutSearchCriteria { SetName = "Not A Real Set" });

            Assert.AreEqual(1, result.TotalCount);
        }

        [TestMethod]
        public async Task SearchGroupedOwnedAsync_ConditionFilter_ExcludesNonMatchingGroups()
        {
            _cardDataRepositoryMock.Setup(r => r.GetCardByID(1)).Returns(new Card { ID = 1, Name = "Dark Magician" });
            _collectionRepositoryMock.Setup(r => r.GetByStatusAsync(CollectionStatus.Owned)).ReturnsAsync(
            [
                new CollectionEntry { ID = 1, CardID = 1, ImageID = 10, SetCode = "LOB-EN001", Condition = CardCondition.NearMint, Quantity = 1 }
            ]);

            var result = await _service.SearchGroupedOwnedAsync(new CollectionSearchCriteria { Condition = CardCondition.Damaged });

            Assert.AreEqual(0, result.TotalCount);
        }

        [TestMethod]
        public async Task SearchGroupedOwnedAsync_IsCheckedOutFilter_MatchesOnlyCheckedOutGroups()
        {
            _cardDataRepositoryMock.Setup(r => r.GetCardByID(1)).Returns(new Card { ID = 1, Name = "Dark Magician" });
            _collectionRepositoryMock.Setup(r => r.GetByStatusAsync(CollectionStatus.Owned)).ReturnsAsync(
            [
                new CollectionEntry { ID = 1, CardID = 1, ImageID = 10, SetCode = "LOB-EN001", Quantity = 1 }
            ]);
            _checkedOutRepositoryMock.Setup(r => r.GetCheckedOutLookupAsync())
                .ReturnsAsync(new Dictionary<(int ImageID, string SetCode, string RarityName), (DateTime Date, int Quantity)>
                {
                    [(10, "LOB-EN001", "")] = (DateTime.UtcNow, 1)
                });

            var result = await _service.SearchGroupedOwnedAsync(new CollectionSearchCriteria { IsCheckedOut = true });

            Assert.AreEqual(1, result.TotalCount);
        }

        [TestMethod]
        public async Task SearchWishlistAsync_Pagination_SlicesResults()
        {
            _cardDataRepositoryMock.Setup(r => r.GetCardByID(1)).Returns(new Card { ID = 1, Name = "Alpha Card" });
            _cardDataRepositoryMock.Setup(r => r.GetCardByID(2)).Returns(new Card { ID = 2, Name = "Zeta Card" });
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CardID = 1, ImageID = 10, SetCode = "AAA-EN001" },
                new PreferredVersion { CardID = 2, ImageID = 20, SetCode = "ZZZ-EN001" }
            ]);

            var result = await _service.SearchWishlistAsync(new WishlistSearchCriteria { Page = 1, PageSize = 1 });

            Assert.AreEqual(2, result.PagedItems.TotalCount);
            Assert.AreEqual(1, result.PagedItems.Items.Count);
        }

        [TestMethod]
        public async Task SearchWishlistAsync_SortByNameDescending_ReversesOrder()
        {
            _cardDataRepositoryMock.Setup(r => r.GetCardByID(1)).Returns(new Card { ID = 1, Name = "Alpha Card" });
            _cardDataRepositoryMock.Setup(r => r.GetCardByID(2)).Returns(new Card { ID = 2, Name = "Zeta Card" });
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CardID = 1, ImageID = 10, SetCode = "AAA-EN001" },
                new PreferredVersion { CardID = 2, ImageID = 20, SetCode = "ZZZ-EN001" }
            ]);

            var result = await _service.SearchWishlistAsync(new WishlistSearchCriteria { SortBy = WishlistSortBy.Name, SortDescending = true });

            Assert.AreEqual("Zeta Card", result.PagedItems.Items[0].CardName);
            Assert.AreEqual("Alpha Card", result.PagedItems.Items[1].CardName);
        }
    }
}
