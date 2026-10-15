using CardCollector.Data.Models;
using CardCollector.DTO;
using CardCollector.ViewModels;
using Moq;

namespace CardCollector.Tests.Services
{
    public partial class CardServiceTests
    {
        [TestMethod]
        public async Task GetCollectionStatsAsync_BuildsRaritySetAndAcquisitionBreakdowns()
        {
            SetUpPrinting(101, "LOB-EN001", "Ultra Rare");
            _collectionRepositoryMock.Setup(r => r.GetByStatusAsync(CollectionStatus.Owned)).ReturnsAsync(
            [
                new CollectionEntry { ID = 1, CardID = 1, CatalogPrintingID = 101, AcquisitionMethod = AcquisitionMethod.Purchased, Quantity = 2 },
                new CollectionEntry { ID = 2, CardID = 1, CatalogPrintingID = 999, AcquisitionMethod = null, Quantity = 1 }
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
        public async Task GetCollectionStatsAsync_MapsCollectionAndWishlistSnapshotsToValueHistory()
        {
            _collectionValueRepositoryMock.Setup(r => r.GetAllSnapshotsAsync()).ReturnsAsync(
            [
                new CollectionValueSnapshot { SnapshotDate = "2026-01-01", TotalValue = 100m, CardCount = 5 }
            ]);
            _wishlistValueRepositoryMock.Setup(r => r.GetAllSnapshotsAsync()).ReturnsAsync(
            [
                new WishlistValueSnapshot { SnapshotDate = "2026-01-01", TotalValue = 50m, RemainingCount = 7 }
            ]);

            var stats = await _service.GetCollectionStatsAsync();

            Assert.AreEqual(1, stats.ValueHistory.Count);
            Assert.AreEqual("2026-01-01", stats.ValueHistory[0].SnapshotDate);
            Assert.AreEqual(100m, stats.ValueHistory[0].TotalValue);
            Assert.AreEqual(5, stats.ValueHistory[0].Count);

            Assert.AreEqual(1, stats.WishlistValueHistory.Count);
            Assert.AreEqual("2026-01-01", stats.WishlistValueHistory[0].SnapshotDate);
            Assert.AreEqual(50m, stats.WishlistValueHistory[0].TotalValue);
            Assert.AreEqual(7, stats.WishlistValueHistory[0].Count);
        }

        [TestMethod]
        public async Task SearchCheckedOutAsync_CardTypeFilter_ExcludesNonMatchingType()
        {
            SetUpCard(1, "Dark Magician", new Set { Code = "LOB-EN001", PrintingID = 101, RarityName = "Ultra Rare" }).CardType = "Normal Monster";
            _checkedOutRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new CheckedOutCard { ID = 1, CardID = 1, CatalogPrintingID = 101, Quantity = 1 }
            ]);

            var result = await _service.SearchCheckedOutAsync(new CheckedOutSearchCriteria { CardType = "Spell" });

            Assert.AreEqual(0, result.TotalCount);
        }

        [TestMethod]
        public async Task SearchCheckedOutAsync_QueryDoesNotMatch_ExcludesResult()
        {
            SetUpPrinting(101, "LOB-EN001", "Ultra Rare");
            _checkedOutRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new CheckedOutCard { ID = 1, CardID = 1, CatalogPrintingID = 101, Quantity = 1 }
            ]);

            var result = await _service.SearchCheckedOutAsync(new CheckedOutSearchCriteria { Query = "Blue-Eyes" });

            Assert.AreEqual(0, result.TotalCount);
        }

        [TestMethod]
        public async Task SearchCheckedOutAsync_QueryFilter_MatchesCardName()
        {
            SetUpPrinting(101, "LOB-EN001", "Ultra Rare");
            _checkedOutRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new CheckedOutCard { ID = 1, CardID = 1, CatalogPrintingID = 101, Quantity = 2 }
            ]);

            var result = await _service.SearchCheckedOutAsync(new CheckedOutSearchCriteria { Query = "Dark" });

            Assert.AreEqual(1, result.TotalCount);
            Assert.AreEqual(2, result.TotalQuantitySum);
        }

        [TestMethod]
        public async Task SearchCheckedOutAsync_RarityNameFilter_ExcludesNonMatchingRarity()
        {
            SetUpPrinting(101, "LOB-EN001", "Ultra Rare");
            _checkedOutRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new CheckedOutCard { ID = 1, CardID = 1, CatalogPrintingID = 101, Quantity = 1 }
            ]);

            var result = await _service.SearchCheckedOutAsync(new CheckedOutSearchCriteria { RarityName = "Common" });

            Assert.AreEqual(0, result.TotalCount);
        }

        [TestMethod]
        public async Task SearchCheckedOutAsync_RarityNameFilterDiffersOnlyByCase_StillMatches()
        {
            SetUpPrinting(101, "LOB-EN001", "Ultra Rare");
            _checkedOutRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new CheckedOutCard { ID = 1, CardID = 1, CatalogPrintingID = 101, Quantity = 1 }
            ]);

            var result = await _service.SearchCheckedOutAsync(new CheckedOutSearchCriteria { RarityName = "ultra rare" });

            Assert.AreEqual(1, result.TotalCount);
        }

        [TestMethod]
        public async Task SearchCheckedOutAsync_SetNameFilter_ExcludesNonMatchingSetPrefix()
        {
            SetUpPrinting(101, "LOB-EN001", "Ultra Rare");
            _cardDataRepositoryMock.Setup(r => r.GetSetPrefixByName("Metal Raiders")).Returns("MRD");
            _checkedOutRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new CheckedOutCard { ID = 1, CardID = 1, CatalogPrintingID = 101, Quantity = 1 }
            ]);

            var result = await _service.SearchCheckedOutAsync(new CheckedOutSearchCriteria { SetName = "Metal Raiders" });

            Assert.AreEqual(0, result.TotalCount);
        }

        [TestMethod]
        public async Task SearchCheckedOutAsync_SetNameFilter_UnknownSetName_DoesNotFilter()
        {
            SetUpPrinting(101, "LOB-EN001", "Ultra Rare");
            _cardDataRepositoryMock.Setup(r => r.GetSetPrefixByName("Not A Real Set")).Returns((string?)null);
            _checkedOutRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new CheckedOutCard { ID = 1, CardID = 1, CatalogPrintingID = 101, Quantity = 1 }
            ]);

            var result = await _service.SearchCheckedOutAsync(new CheckedOutSearchCriteria { SetName = "Not A Real Set" });

            Assert.AreEqual(1, result.TotalCount);
        }

        [TestMethod]
        public async Task SearchGroupedOwnedAsync_ConditionFilter_ExcludesNonMatchingGroups()
        {
            SetUpPrinting(101, "LOB-EN001", "Ultra Rare");
            _collectionRepositoryMock.Setup(r => r.GetByStatusAsync(CollectionStatus.Owned)).ReturnsAsync(
            [
                new CollectionEntry { ID = 1, CardID = 1, CatalogPrintingID = 101, Condition = CardCondition.NearMint, Quantity = 1 }
            ]);

            var result = await _service.SearchGroupedOwnedAsync(new CollectionSearchCriteria { Condition = CardCondition.Damaged });

            Assert.AreEqual(0, result.TotalCount);
        }

        [TestMethod]
        public async Task SearchGroupedOwnedAsync_IsCheckedOutFilter_MatchesOnlyCheckedOutGroups()
        {
            SetUpPrinting(101, "LOB-EN001", "Ultra Rare");
            _collectionRepositoryMock.Setup(r => r.GetByStatusAsync(CollectionStatus.Owned)).ReturnsAsync(
            [
                new CollectionEntry { ID = 1, CardID = 1, CatalogPrintingID = 101, Quantity = 1 }
            ]);
            _checkedOutRepositoryMock.Setup(r => r.GetCheckedOutLookupAsync())
                .ReturnsAsync(new Dictionary<int, (DateTime Date, int Quantity)>
                {
                    [101] = (DateTime.UtcNow, 1)
                });

            var result = await _service.SearchGroupedOwnedAsync(new CollectionSearchCriteria { IsCheckedOut = true });

            Assert.AreEqual(1, result.TotalCount);
        }

        [TestMethod]
        public async Task SearchWishlistAsync_Pagination_SlicesResults()
        {
            SetUpCard(1, "Alpha Card", new Set { Code = "AAA-EN001", PrintingID = 101 });
            SetUpCard(2, "Zeta Card", new Set { Code = "ZZZ-EN001", PrintingID = 201 });
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CardID = 1, CatalogPrintingID = 101 },
                new PreferredVersion { CardID = 2, CatalogPrintingID = 201 }
            ]);

            var result = await _service.SearchWishlistAsync(new WishlistSearchCriteria { Page = 1, PageSize = 1 });

            Assert.AreEqual(2, result.PagedItems.TotalCount);
            Assert.AreEqual(1, result.PagedItems.Items.Count);
        }

        [TestMethod]
        public async Task SearchWishlistAsync_SortByNameDescending_ReversesOrder()
        {
            SetUpCard(1, "Alpha Card", new Set { Code = "AAA-EN001", PrintingID = 101 });
            SetUpCard(2, "Zeta Card", new Set { Code = "ZZZ-EN001", PrintingID = 201 });
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(
            [
                new PreferredVersion { CardID = 1, CatalogPrintingID = 101 },
                new PreferredVersion { CardID = 2, CatalogPrintingID = 201 }
            ]);

            var result = await _service.SearchWishlistAsync(new WishlistSearchCriteria { SortBy = WishlistSortBy.Name, SortDescending = true });

            Assert.AreEqual("Zeta Card", result.PagedItems.Items[0].CardName);
            Assert.AreEqual("Alpha Card", result.PagedItems.Items[1].CardName);
        }
    }
}
