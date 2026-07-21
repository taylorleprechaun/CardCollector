using CardCollector.Data.Models;
using CardCollector.DTO;
using CardCollector.Repository;
using CardCollector.Services;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CardCollector.Tests.Services
{
    [TestClass]
    public partial class CardServiceTests
    {
        private Mock<ICardDataRepository> _cardDataRepositoryMock = null!;
        private Mock<ICardSetRepository> _cardSetRepositoryMock = null!;
        private Mock<ICheckedOutRepository> _checkedOutRepositoryMock = null!;
        private Mock<ICollectionEntryValueRepository> _collectionEntryValueRepositoryMock = null!;
        private Mock<ICollectionRepository> _collectionRepositoryMock = null!;
        private Mock<ICollectionValueRepository> _collectionValueRepositoryMock = null!;
        private Mock<IDismissedNewPrintingRepository> _dismissedNewPrintingRepositoryMock = null!;
        private Mock<IIgnoredCardRepository> _ignoredCardRepositoryMock = null!;
        private Mock<IPendingOrderRepository> _pendingOrderRepositoryMock = null!;
        private Mock<IPreferredVersionRepository> _preferredVersionRepositoryMock = null!;
        private Mock<IPricingService> _pricingServiceMock = null!;
        private CardService _service = null!;
        private Mock<IUnitOfWork> _unitOfWorkMock = null!;
        [TestInitialize]
        public void Setup()
        {
            _cardDataRepositoryMock = new Mock<ICardDataRepository>();
            _cardSetRepositoryMock = new Mock<ICardSetRepository>();
            _checkedOutRepositoryMock = new Mock<ICheckedOutRepository>();
            _collectionRepositoryMock = new Mock<ICollectionRepository>();
            _collectionEntryValueRepositoryMock = new Mock<ICollectionEntryValueRepository>();
            _collectionValueRepositoryMock = new Mock<ICollectionValueRepository>();
            _dismissedNewPrintingRepositoryMock = new Mock<IDismissedNewPrintingRepository>();
            _ignoredCardRepositoryMock = new Mock<IIgnoredCardRepository>();
            _pendingOrderRepositoryMock = new Mock<IPendingOrderRepository>();
            _preferredVersionRepositoryMock = new Mock<IPreferredVersionRepository>();
            _pricingServiceMock = new Mock<IPricingService>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();

            // Safe defaults so tests that don't care about these still don't NRE on null.
            _cardDataRepositoryMock.Setup(r => r.GetSetNamesByCode()).Returns(new Dictionary<string, string>());
            _cardDataRepositoryMock.Setup(r => r.GetBrowseableCards()).Returns(Enumerable.Empty<Card>());
            _cardDataRepositoryMock.Setup(r => r.GetAllCards()).Returns(Enumerable.Empty<Card>());
            _unitOfWorkMock
                .Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>()))
                .Returns((Func<Task> op) => op());

            _preferredVersionRepositoryMock.Setup(r => r.GetByImageIDsAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync(new Dictionary<int, PreferredVersion>());
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<PreferredVersion>());
            _preferredVersionRepositoryMock.Setup(r => r.GetPreferredCardIDsAsync()).ReturnsAsync(new HashSet<int>());
            _checkedOutRepositoryMock.Setup(r => r.GetCheckedOutLookupAsync())
                .ReturnsAsync(new Dictionary<(int ImageID, string SetCode, string RarityName), (DateTime Date, int Quantity)>());
            _dismissedNewPrintingRepositoryMock.Setup(r => r.GetAllAsync())
                .ReturnsAsync(new HashSet<(int CardID, string SetCode, string RarityName)>());
            _ignoredCardRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new Dictionary<int, DateTime>());
            _ignoredCardRepositoryMock.Setup(r => r.GetIgnoredCardIDsAsync()).ReturnsAsync(new HashSet<int>());
            _pendingOrderRepositoryMock.Setup(r => r.GetStagedQuantitiesAsync())
                .ReturnsAsync(new Dictionary<(int ImageID, string SetCode, string RarityName), int>());
            _collectionRepositoryMock.Setup(r => r.GetOrderedQuantitiesAsync())
                .ReturnsAsync(new Dictionary<(int ImageID, string SetCode, string RarityName), int>());
            _collectionRepositoryMock.Setup(r => r.GetOwnedQuantitiesForPreferredVersionsAsync(It.IsAny<IEnumerable<(int ImageID, string SetCode, string? RarityName)>>()))
                .ReturnsAsync(new Dictionary<(int ImageID, string SetCode), int>());
            _collectionRepositoryMock.Setup(r => r.GetOwnedStatsAsync()).ReturnsAsync(new OwnedCollectionStats(0, null, null));
            _collectionRepositoryMock.Setup(r => r.GetOwnedPairsAsync()).ReturnsAsync(new HashSet<(int, string)>());
            _collectionRepositoryMock.Setup(r => r.GetStatusByCardIDsAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync(new Dictionary<int, CollectionStatus>());
            _collectionRepositoryMock.Setup(r => r.GetCompletionStatusByImageIDsAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync(new Dictionary<int, CollectionCompletionStatus>());
            _pricingServiceMock.Setup(p => p.GetCardEditionMapAsync(It.IsAny<int>()))
                .ReturnsAsync(new Dictionary<(string SetCode, string RarityName), IReadOnlySet<CardEdition>>());
            _collectionRepositoryMock.Setup(r => r.GetOwnedQuantitiesForPairsAsync(It.IsAny<IEnumerable<(int ImageID, string SetCode, string RarityName)>>()))
                .ReturnsAsync(new Dictionary<(int ImageID, string SetCode, string RarityName), int>());

            _service = new CardService(
                _cardDataRepositoryMock.Object,
                _cardSetRepositoryMock.Object,
                _checkedOutRepositoryMock.Object,
                _collectionRepositoryMock.Object,
                _collectionEntryValueRepositoryMock.Object,
                _collectionValueRepositoryMock.Object,
                _dismissedNewPrintingRepositoryMock.Object,
                _ignoredCardRepositoryMock.Object,
                Mock.Of<ILogger<CardService>>(),
                _pendingOrderRepositoryMock.Object,
                _preferredVersionRepositoryMock.Object,
                _pricingServiceMock.Object,
                _unitOfWorkMock.Object);
        }
    }
}
