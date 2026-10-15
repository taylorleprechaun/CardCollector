using CardCollector.Data.Models;
using CardCollector.DTO;
using CardCollector.Models.Catalog;
using CardCollector.Repository;
using CardCollector.Services;
using Microsoft.Extensions.Logging;
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
        private Mock<IWishlistValueRepository> _wishlistValueRepositoryMock = null!;
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
            _wishlistValueRepositoryMock = new Mock<IWishlistValueRepository>();

            // Safe defaults so tests that don't care about these still don't NRE on null.
            _cardDataRepositoryMock.Setup(r => r.GetSetNamesByCode()).Returns(new Dictionary<string, string>());
            _cardDataRepositoryMock.Setup(r => r.GetBrowseableCards()).Returns(Enumerable.Empty<Card>());
            _cardDataRepositoryMock.Setup(r => r.GetAllCards()).Returns(Enumerable.Empty<Card>());
            _unitOfWorkMock
                .Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
                .Returns((Func<Task> op, CancellationToken _) => op());

            _preferredVersionRepositoryMock.Setup(r => r.GetByCardIDsAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync(new Dictionary<int, IReadOnlyList<PreferredVersion>>());
            _preferredVersionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<PreferredVersion>());
            _preferredVersionRepositoryMock.Setup(r => r.GetByCardIDAsync(It.IsAny<int>())).ReturnsAsync(new List<PreferredVersion>());
            _preferredVersionRepositoryMock.Setup(r => r.GetPreferredCardIDsAsync()).ReturnsAsync(new HashSet<int>());
            _checkedOutRepositoryMock.Setup(r => r.GetCheckedOutLookupAsync())
                .ReturnsAsync(new Dictionary<int, (DateTime Date, int Quantity)>());
            _dismissedNewPrintingRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new HashSet<int>());
            _ignoredCardRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new Dictionary<int, DateTime>());
            _ignoredCardRepositoryMock.Setup(r => r.GetIgnoredCardIDsAsync()).ReturnsAsync(new HashSet<int>());
            _pendingOrderRepositoryMock.Setup(r => r.GetStagedQuantitiesAsync()).ReturnsAsync(new Dictionary<int, int>());
            _collectionRepositoryMock.Setup(r => r.GetOrderedQuantitiesAsync()).ReturnsAsync(new Dictionary<int, int>());
            _collectionRepositoryMock.Setup(r => r.GetOwnedStatsAsync()).ReturnsAsync(new OwnedCollectionStats(0, null, null));
            _collectionRepositoryMock.Setup(r => r.GetOwnedPrintingIDsAsync()).ReturnsAsync(new HashSet<int>());
            _collectionRepositoryMock.Setup(r => r.GetStatusByCardIDsAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync(new Dictionary<int, CollectionStatus>());
            _collectionRepositoryMock.Setup(r => r.GetCompletionStatusByCardIDsAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync(new Dictionary<int, CollectionCompletionStatus>());
            _pricingServiceMock.Setup(p => p.GetEditionsByPrintingAsync(It.IsAny<int>()))
                .ReturnsAsync(new Dictionary<int, IReadOnlySet<CardEdition>>());
            _collectionRepositoryMock.Setup(r => r.GetOwnedQuantitiesByPrintingAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync(new Dictionary<int, int>());
            _wishlistValueRepositoryMock.Setup(r => r.GetAllSnapshotsAsync()).ReturnsAsync(new List<WishlistValueSnapshot>());
            _wishlistValueRepositoryMock.Setup(r => r.GetLatestSnapshotAsync()).ReturnsAsync((WishlistValueSnapshot?)null);

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
                _unitOfWorkMock.Object,
                _wishlistValueRepositoryMock.Object);
        }

        /// <summary>Registers a card and each of its printings (and the printings' card ID) with the card data mock, and returns the card.</summary>
        private Card SetUpCard(int cardID, string name, params Set[] printings)
        {
            var card = new Card { ID = cardID, Name = name, CardSets = printings };
            _cardDataRepositoryMock.Setup(r => r.GetCardByID(cardID)).Returns(card);
            foreach (var printing in printings)
            {
                _cardDataRepositoryMock.Setup(r => r.GetCardIDByPrintingID(printing.PrintingID)).Returns(cardID);
                _cardDataRepositoryMock.Setup(r => r.GetPrintingByID(printing.PrintingID)).Returns(new CatalogPrintingRef(card, printing));
            }
            return card;
        }

        private void SetUpPrinting(int printingID, string setCode, string rarityName, string? printVariant = null) =>
            SetUpCard(1, "Dark Magician", new Set { Code = setCode, PrintingID = printingID, PrintVariant = printVariant, RarityName = rarityName });
    }
}
