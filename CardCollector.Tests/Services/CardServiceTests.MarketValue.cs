using CardCollector.Data.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CardCollector.Tests.Services
{
    public partial class CardServiceTests
    {
        private static readonly string Today = DateTime.UtcNow.ToString("yyyy-MM-dd");

        [TestMethod]
        public async Task CalculateCurrentMarketValueAsync_EntryWithBlankRarityName_IsExcludedFromTotal()
        {
            _collectionValueRepositoryMock.Setup(r => r.GetLatestSnapshotAsync()).ReturnsAsync((CollectionValueSnapshot?)null);
            _collectionRepositoryMock.Setup(r => r.GetByStatusAsync(CollectionStatus.Owned)).ReturnsAsync(
            [
                new CollectionEntry { ID = 1, CardID = 1, SetCode = "LOB-EN001", RarityName = null, Quantity = 1, MarketPriceAtEntry = 10m }
            ]);

            var (totalValue, _, _, _) = await _service.CalculateCurrentMarketValueAsync();

            Assert.AreEqual(0m, totalValue);
            _pricingServiceMock.Verify(p => p.GetPrintingPriceAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CardEdition?>()), Times.Never);
        }

        [TestMethod]
        public async Task CalculateCurrentMarketValueAsync_NoPriceNoSnapshotNoMarketPriceAtEntry_EntryIsSkipped()
        {
            _collectionValueRepositoryMock.Setup(r => r.GetLatestSnapshotAsync()).ReturnsAsync((CollectionValueSnapshot?)null);
            _collectionRepositoryMock.Setup(r => r.GetByStatusAsync(CollectionStatus.Owned)).ReturnsAsync(
            [
                new CollectionEntry { ID = 1, CardID = 1, SetCode = "LOB-EN001", RarityName = "Ultra Rare", Quantity = 1, MarketPriceAtEntry = null }
            ]);
            _pricingServiceMock
                .Setup(p => p.GetPrintingPriceAsync(1, "LOB-EN001", "Ultra Rare", null))
                .ReturnsAsync((decimal?)null);

            var (totalValue, _, _, _) = await _service.CalculateCurrentMarketValueAsync();

            Assert.AreEqual(0m, totalValue);
        }

        [TestMethod]
        public async Task CalculateCurrentMarketValueAsync_NoSnapshotYet_FetchesLivePricesAndPersistsSnapshot()
        {
            _collectionValueRepositoryMock.Setup(r => r.GetLatestSnapshotAsync()).ReturnsAsync((CollectionValueSnapshot?)null);
            _collectionRepositoryMock.Setup(r => r.GetByStatusAsync(CollectionStatus.Owned)).ReturnsAsync(
            [
                new CollectionEntry { ID = 1, CardID = 1, SetCode = "LOB-EN001", RarityName = "Ultra Rare", Edition = CardEdition.FirstEdition, Quantity = 2 }
            ]);
            _pricingServiceMock.Setup(p => p.GetPrintingPriceAsync(1, "LOB-EN001", "Ultra Rare", CardEdition.FirstEdition)).ReturnsAsync(10.00m);

            var progressCalls = new List<(int Current, int Total)>();

            var (totalValue, cardCount, _, _) = await _service.CalculateCurrentMarketValueAsync(
                (current, total) => { progressCalls.Add((current, total)); return Task.CompletedTask; });

            Assert.AreEqual(20.00m, totalValue);
            Assert.AreEqual(2, cardCount);
            CollectionAssert.AreEqual(new[] { (1, 1) }, progressCalls);
            _collectionEntryValueRepositoryMock.Verify(r => r.UpsertSnapshotsAsync(It.IsAny<IEnumerable<CollectionEntryValueSnapshot>>(), Today), Times.Once);
            _collectionValueRepositoryMock.Verify(r => r.UpsertSnapshotAsync(It.Is<CollectionValueSnapshot>(s => s.TotalValue == 20.00m && s.CardCount == 2)), Times.Once);
        }

        [TestMethod]
        public async Task CalculateCurrentMarketValueAsync_PriceUnavailableButPreviousSnapshotExists_FallsBackToPreviousValue()
        {
            _collectionValueRepositoryMock.Setup(r => r.GetLatestSnapshotAsync()).ReturnsAsync((CollectionValueSnapshot?)null);
            _collectionRepositoryMock.Setup(r => r.GetByStatusAsync(CollectionStatus.Owned)).ReturnsAsync(
            [
                new CollectionEntry { ID = 1, CardID = 1, SetCode = "LOB-EN001", RarityName = "Ultra Rare", Quantity = 1 }
            ]);
            _collectionEntryValueRepositoryMock.Setup(r => r.GetLatestSnapshotsAsync()).ReturnsAsync(
            [
                new CollectionEntryValueSnapshot { CollectionEntryID = 1, MarketValue = 42m }
            ]);
            _pricingServiceMock
                .Setup(p => p.GetPrintingPriceAsync(1, "LOB-EN001", "Ultra Rare", null))
                .ReturnsAsync((decimal?)null);

            var (totalValue, _, _, _) = await _service.CalculateCurrentMarketValueAsync();

            Assert.AreEqual(42m, totalValue);
        }

        [TestMethod]
        public async Task CalculateCurrentMarketValueAsync_PriceUnavailableNoSnapshotButMarketPriceAtEntryExists_FallsBackToThat()
        {
            _collectionValueRepositoryMock.Setup(r => r.GetLatestSnapshotAsync()).ReturnsAsync((CollectionValueSnapshot?)null);
            _collectionRepositoryMock.Setup(r => r.GetByStatusAsync(CollectionStatus.Owned)).ReturnsAsync(
            [
                new CollectionEntry { ID = 1, CardID = 1, SetCode = "LOB-EN001", RarityName = "Ultra Rare", Quantity = 2, MarketPriceAtEntry = 7m }
            ]);
            _pricingServiceMock
                .Setup(p => p.GetPrintingPriceAsync(1, "LOB-EN001", "Ultra Rare", null))
                .ReturnsAsync((decimal?)null);

            var (totalValue, _, _, _) = await _service.CalculateCurrentMarketValueAsync();

            Assert.AreEqual(14m, totalValue);
        }

        [TestMethod]
        public async Task CalculateCurrentMarketValueAsync_SnapshotFromToday_UsesCacheWithoutRefetchingPrices()
        {
            _collectionValueRepositoryMock.Setup(r => r.GetLatestSnapshotAsync())
                .ReturnsAsync(new CollectionValueSnapshot { SnapshotDate = Today, TotalValue = 100m, CardCount = 5 });
            _collectionEntryValueRepositoryMock.Setup(r => r.GetLatestSnapshotsAsync()).ReturnsAsync(
            [
                new CollectionEntryValueSnapshot { CollectionEntryID = 1, CardName = "Dark Magician", SetName = "LOB", RarityName = "Ultra Rare", MarketValue = 30m, SnapshotDate = Today }
            ]);
            _collectionRepositoryMock.Setup(r => r.GetByStatusAsync(CollectionStatus.Owned)).ReturnsAsync(
            [
                new CollectionEntry { ID = 1, CardID = 1, Quantity = 1 }
            ]);

            var (totalValue, cardCount, setBreakdown, topCards) = await _service.CalculateCurrentMarketValueAsync();

            Assert.AreEqual(100m, totalValue);
            Assert.AreEqual(5, cardCount);
            Assert.AreEqual(1, setBreakdown.Count);
            Assert.AreEqual(1, topCards.Count);
            _pricingServiceMock.Verify(p => p.GetPrintingPriceAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CardEdition?>()), Times.Never);
        }
        [TestMethod]
        public async Task GetCardPriceHistoryAsync_BlankCardName_ThrowsArgumentException()
        {
            await Assert.ThrowsExactlyAsync<ArgumentException>(() => _service.GetCardPriceHistoryAsync("  "));
        }

        [TestMethod]
        public async Task GetCardPriceHistoryAsync_GroupsSnapshotsByEntryAndComputesPerUnitValue()
        {
            _collectionEntryValueRepositoryMock.Setup(r => r.GetHistoryByCardNameAsync("Dark Magician")).ReturnsAsync(
            [
                new CollectionEntryValueSnapshot { CollectionEntryID = 1, SetCode = "LOB-EN001", RarityName = "Ultra Rare", MarketValue = 20m, SnapshotDate = "2026-01-01" }
            ]);
            _collectionRepositoryMock.Setup(r => r.GetByStatusAsync(CollectionStatus.Owned)).ReturnsAsync(
            [
                new CollectionEntry { ID = 1, Quantity = 2 }
            ]);

            var result = await _service.GetCardPriceHistoryAsync("Dark Magician");

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("LOB-EN001 — Ultra Rare", result[0].Label);
            Assert.AreEqual(10m, result[0].Values[0]);
        }

        [TestMethod]
        public async Task GetCardPriceHistoryAsync_NoOwnedQuantityFound_TreatsQuantityAsOne()
        {
            _collectionEntryValueRepositoryMock.Setup(r => r.GetHistoryByCardNameAsync("Dark Magician")).ReturnsAsync(
            [
                new CollectionEntryValueSnapshot { CollectionEntryID = 1, SetCode = "LOB-EN001", RarityName = "Ultra Rare", MarketValue = 20m, SnapshotDate = "2026-01-01" }
            ]);

            var result = await _service.GetCardPriceHistoryAsync("Dark Magician");

            Assert.AreEqual(20m, result[0].Values[0]);
        }
    }
}
