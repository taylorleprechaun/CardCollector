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
        public async Task CheckEntryEditionAsync_NoEditionDataForPrinting_ReturnsUnverifiable()
        {
            var result = await _service.CheckEntryEditionAsync(1, "LOB-EN001", "Ultra Rare", CardEdition.FirstEdition);

            Assert.AreEqual(EditionAuditCategory.Unverifiable, result);
        }

        [TestMethod]
        public async Task CheckEntryEditionAsync_RecordedEditionMatchesLiveData_ReturnsNull()
        {
            _pricingServiceMock.Setup(p => p.GetCardEditionMapAsync(1)).ReturnsAsync(
                new Dictionary<(string SetCode, string RarityName), IReadOnlySet<CardEdition>>
                {
                    [("LOB-EN001", "ULTRA RARE")] = new HashSet<CardEdition> { CardEdition.FirstEdition }
                });

            var result = await _service.CheckEntryEditionAsync(1, "LOB-EN001", "Ultra Rare", CardEdition.FirstEdition);

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task CheckEntryEditionAsync_RecordedEditionNotInLiveData_ReturnsEditionMismatch()
        {
            _pricingServiceMock.Setup(p => p.GetCardEditionMapAsync(1)).ReturnsAsync(
                new Dictionary<(string SetCode, string RarityName), IReadOnlySet<CardEdition>>
                {
                    [("LOB-EN001", "ULTRA RARE")] = new HashSet<CardEdition> { CardEdition.Unlimited }
                });

            var result = await _service.CheckEntryEditionAsync(1, "LOB-EN001", "Ultra Rare", CardEdition.FirstEdition);

            Assert.AreEqual(EditionAuditCategory.EditionMismatch, result);
        }
        [TestMethod]
        public async Task GetEnrichedOrdersAsync_EntryWithEditionMismatch_FlagsCategory()
        {
            _collectionRepositoryMock.Setup(r => r.GetByStatusAsync(CollectionStatus.Ordered)).ReturnsAsync(
            [
                new CollectionEntry { ID = 1, CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = "Ultra Rare", Edition = CardEdition.FirstEdition, Quantity = 1 }
            ]);
            _pricingServiceMock.Setup(p => p.GetCardEditionMapAsync(1)).ReturnsAsync(
                new Dictionary<(string SetCode, string RarityName), IReadOnlySet<CardEdition>>
                {
                    [("LOB-EN001", "ULTRA RARE")] = new HashSet<CardEdition> { CardEdition.Unlimited }
                });

            var result = (await _service.GetEnrichedOrdersAsync()).ToList();

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(EditionAuditCategory.EditionMismatch, result[0].Category);
        }

        [TestMethod]
        public async Task GetEnrichedOrdersAsync_EntryWithNoRecordedEdition_SkipsCategoryCheck()
        {
            _collectionRepositoryMock.Setup(r => r.GetByStatusAsync(CollectionStatus.Ordered)).ReturnsAsync(
            [
                new CollectionEntry { ID = 1, CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = "Ultra Rare", Edition = null, Quantity = 1 }
            ]);

            var result = (await _service.GetEnrichedOrdersAsync()).ToList();

            Assert.IsNull(result[0].Category);
            Assert.AreEqual(0, result[0].AvailableEditions.Count);
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

        private void SetUpEditionMismatchEntry()
        {
            _cardDataRepositoryMock.Setup(r => r.GetCardByID(1)).Returns(new Card
            {
                ID = 1,
                Name = "Dark Magician",
                CardSets = [new Set { Code = "LOB-EN001", RarityName = "Ultra Rare", RarityCode = "(UR)" }]
            });
            var entries = new List<CollectionEntry>
            {
                new() { ID = 1, CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = "Ultra Rare", Edition = CardEdition.FirstEdition, Quantity = 1, Status = CollectionStatus.Owned }
            };
            _collectionRepositoryMock.Setup(r => r.GetByStatusAsync(CollectionStatus.Owned)).ReturnsAsync(entries);
            _collectionRepositoryMock.Setup(r => r.GetByCardIDAsync(1)).ReturnsAsync(entries);
            _pricingServiceMock.Setup(p => p.GetCardEditionMapAsync(1)).ReturnsAsync(
                new Dictionary<(string SetCode, string RarityName), IReadOnlySet<CardEdition>>
                {
                    [("LOB-EN001", "ULTRA RARE")] = new HashSet<CardEdition> { CardEdition.Unlimited }
                });
        }
    }
}
