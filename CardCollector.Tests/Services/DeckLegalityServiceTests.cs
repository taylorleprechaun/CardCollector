using CardCollector.DTO;
using CardCollector.Data.Models;
using CardCollector.Models;
using CardCollector.Repository;
using CardCollector.Services;
using CardCollector.ViewModels;
using Moq;

namespace CardCollector.Tests.Services
{
    [TestClass]
    public sealed class DeckLegalityServiceTests
    {
        private const int RESTRICTED_CARD_ID = 1;
        private const int RESTRICTED_KONAMI_ID = 100;

        [TestMethod]
        public async Task GetAsync_AtEventViewForcedWithNoEvents_ReturnsNoCardStatuses()
        {
            var deck = BuildDeck([]);
            var repo = new Mock<IBanlistRepository>();
            repo.Setup(r => r.GetAvailableListsAsync(It.IsAny<CancellationToken>())).ReturnsAsync((IReadOnlyList<DateOnly>)[new DateOnly(2024, 1, 1)]);
            var service = new DeckLegalityService(repo.Object);

            var result = await service.GetAsync(deck, DeckLegalityView.AtEvent, eventID: null, listDate: null);

            Assert.IsNull(result.CardStatuses);
            Assert.IsNull(result.AtEventSource);
        }

        [TestMethod]
        public async Task GetAsync_DeckWithNoEvents_DefaultsToCurrentView()
        {
            var deck = BuildDeck([]);
            var current = BuildBanlist(new DateOnly(2026, 1, 1));
            var repo = new Mock<IBanlistRepository>();
            repo.Setup(r => r.GetCurrentAsync(It.IsAny<CancellationToken>())).ReturnsAsync(current);
            repo.Setup(r => r.GetAvailableListsAsync(It.IsAny<CancellationToken>())).ReturnsAsync((IReadOnlyList<DateOnly>)[current.EffectiveDate]);
            var service = new DeckLegalityService(repo.Object);

            var result = await service.GetAsync(deck, view: null, eventID: null, listDate: null);

            Assert.AreEqual(DeckLegalityView.Current, result.ActiveView);
            Assert.IsNull(result.AtEventSource);
        }

        [TestMethod]
        public async Task GetAsync_EventDateBeforeFirstList_CardStatusesIsNull()
        {
            var oldEvent = new Event { ID = 1, Date = new DateOnly(1990, 1, 1), Location = "Ancient" };
            var deck = BuildDeck([oldEvent]);
            var repo = new Mock<IBanlistRepository>();
            repo.Setup(r => r.GetListForDateAsync(oldEvent.Date, It.IsAny<CancellationToken>())).ReturnsAsync((Banlist?)null);
            repo.Setup(r => r.GetAvailableListsAsync(It.IsAny<CancellationToken>())).ReturnsAsync((IReadOnlyList<DateOnly>)[new DateOnly(2000, 1, 1)]);
            var service = new DeckLegalityService(repo.Object);

            var result = await service.GetAsync(deck, DeckLegalityView.AtEvent, eventID: null, listDate: null);

            Assert.IsTrue(result.IsAvailable);
            Assert.IsNull(result.CardStatuses);
        }

        [TestMethod]
        public async Task GetAsync_EventDateResolvesCorrectList()
        {
            var tournamentEvent = new Event { ID = 1, Date = new DateOnly(2024, 6, 1), Location = "Locals" };
            var deck = BuildDeck([tournamentEvent]);
            var list = BuildBanlist(new DateOnly(2024, 5, 1), BanlistLimit.Limited);
            var repo = new Mock<IBanlistRepository>();
            repo.Setup(r => r.GetListForDateAsync(tournamentEvent.Date, It.IsAny<CancellationToken>())).ReturnsAsync(list);
            repo.Setup(r => r.GetAvailableListsAsync(It.IsAny<CancellationToken>())).ReturnsAsync((IReadOnlyList<DateOnly>)[list.EffectiveDate]);
            var service = new DeckLegalityService(repo.Object);

            var result = await service.GetAsync(deck, view: null, eventID: null, listDate: null);

            Assert.AreEqual(DeckLegalityView.AtEvent, result.ActiveView);
            Assert.AreEqual(BanlistLimit.Limited, result.CardStatuses![RESTRICTED_CARD_ID].Limit);
        }

        [TestMethod]
        public async Task GetAsync_EventIDMatchesEvent_UsesThatEventRatherThanMostRecent()
        {
            var recent = new Event { ID = 2, Date = new DateOnly(2024, 6, 1), Location = "Recent" };
            var older = new Event { ID = 1, Date = new DateOnly(2024, 1, 1), Location = "Older" };
            var deck = BuildDeck([recent, older]);
            var list = BuildBanlist(new DateOnly(2023, 12, 1));
            var repo = new Mock<IBanlistRepository>();
            repo.Setup(r => r.GetListForDateAsync(older.Date, It.IsAny<CancellationToken>())).ReturnsAsync(list);
            repo.Setup(r => r.GetAvailableListsAsync(It.IsAny<CancellationToken>())).ReturnsAsync((IReadOnlyList<DateOnly>)[list.EffectiveDate]);
            var service = new DeckLegalityService(repo.Object);

            var result = await service.GetAsync(deck, DeckLegalityView.AtEvent, eventID: older.ID, listDate: null);

            Assert.AreEqual(older.ID, result.AtEventSource!.ID);
        }

        [TestMethod]
        public async Task GetAsync_ListDateOverride_UsesRequestedListInsteadOfResolved()
        {
            var deck = BuildDeck([]);
            var overrideDate = new DateOnly(2022, 5, 17);
            var overrideList = BuildBanlist(overrideDate, BanlistLimit.SemiLimited);
            var repo = new Mock<IBanlistRepository>();
            repo.Setup(r => r.GetCurrentAsync(It.IsAny<CancellationToken>())).ReturnsAsync(BuildBanlist(new DateOnly(2026, 1, 1), BanlistLimit.Limited));
            repo.Setup(r => r.GetListAsync(overrideDate, It.IsAny<CancellationToken>())).ReturnsAsync(overrideList);
            repo.Setup(r => r.GetAvailableListsAsync(It.IsAny<CancellationToken>())).ReturnsAsync((IReadOnlyList<DateOnly>)[overrideDate]);
            var service = new DeckLegalityService(repo.Object);

            var result = await service.GetAsync(deck, DeckLegalityView.Current, eventID: null, listDate: overrideDate);

            Assert.AreEqual(BanlistLimit.SemiLimited, result.CardStatuses![RESTRICTED_CARD_ID].Limit);
        }

        [TestMethod]
        public async Task GetAsync_ListDateOverrideOnAtEventView_UsesRequestedList()
        {
            var tournamentEvent = new Event { ID = 1, Date = new DateOnly(2024, 6, 1), Location = "Locals" };
            var deck = BuildDeck([tournamentEvent]);
            var overrideDate = new DateOnly(2022, 5, 17);
            var overrideList = BuildBanlist(overrideDate, BanlistLimit.SemiLimited);
            var repo = new Mock<IBanlistRepository>();
            repo.Setup(r => r.GetListAsync(overrideDate, It.IsAny<CancellationToken>())).ReturnsAsync(overrideList);
            repo.Setup(r => r.GetAvailableListsAsync(It.IsAny<CancellationToken>())).ReturnsAsync((IReadOnlyList<DateOnly>)[overrideDate]);
            var service = new DeckLegalityService(repo.Object);

            var result = await service.GetAsync(deck, DeckLegalityView.AtEvent, eventID: null, listDate: overrideDate);

            Assert.AreEqual(BanlistLimit.SemiLimited, result.CardStatuses![RESTRICTED_CARD_ID].Limit);
        }

        [TestMethod]
        public async Task GetAsync_ListDateRequested_IsEchoedOnViewModel()
        {
            var deck = BuildDeck([]);
            var requestedDate = new DateOnly(2022, 5, 17);
            var repo = new Mock<IBanlistRepository>();
            repo.Setup(r => r.GetListAsync(requestedDate, It.IsAny<CancellationToken>())).ReturnsAsync(BuildBanlist(requestedDate));
            repo.Setup(r => r.GetAvailableListsAsync(It.IsAny<CancellationToken>())).ReturnsAsync((IReadOnlyList<DateOnly>)[requestedDate]);
            var service = new DeckLegalityService(repo.Object);

            var result = await service.GetAsync(deck, DeckLegalityView.Current, eventID: null, listDate: requestedDate);

            Assert.AreEqual(requestedDate, result.RequestedListDate);
        }

        [TestMethod]
        public async Task GetAsync_NoBanlistData_ReportsUnavailable()
        {
            var deck = BuildDeck([]);
            var repo = new Mock<IBanlistRepository>();
            repo.Setup(r => r.GetCurrentAsync(It.IsAny<CancellationToken>())).ReturnsAsync((Banlist?)null);
            repo.Setup(r => r.GetAvailableListsAsync(It.IsAny<CancellationToken>())).ReturnsAsync((IReadOnlyList<DateOnly>)[]);
            var service = new DeckLegalityService(repo.Object);

            var result = await service.GetAsync(deck, view: null, eventID: null, listDate: null);

            Assert.IsFalse(result.IsAvailable);
            Assert.IsNull(result.CardStatuses);
        }

        [TestMethod]
        public async Task GetAsync_NoListDateRequested_ViewModelRequestedListDateIsNull()
        {
            var deck = BuildDeck([]);
            var current = BuildBanlist(new DateOnly(2026, 1, 1));
            var repo = new Mock<IBanlistRepository>();
            repo.Setup(r => r.GetCurrentAsync(It.IsAny<CancellationToken>())).ReturnsAsync(current);
            repo.Setup(r => r.GetAvailableListsAsync(It.IsAny<CancellationToken>())).ReturnsAsync((IReadOnlyList<DateOnly>)[current.EffectiveDate]);
            var service = new DeckLegalityService(repo.Object);

            var result = await service.GetAsync(deck, DeckLegalityView.Current, eventID: null, listDate: null);

            Assert.IsNull(result.RequestedListDate);
        }

        [TestMethod]
        public async Task GetAsync_NullDeck_ThrowsArgumentNullException()
        {
            var service = new DeckLegalityService(new Mock<IBanlistRepository>().Object);

            await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => service.GetAsync(null!, null, null, null));
        }

        [TestMethod]
        public async Task GetAsync_TokenGiven_PassesItToEveryBanlistLookup()
        {
            var requestedDate = new DateOnly(2024, 4, 15);
            using var source = new CancellationTokenSource();
            var token = source.Token;
            var repo = new Mock<IBanlistRepository>();
            repo.Setup(r => r.GetListAsync(requestedDate, token)).ReturnsAsync(BuildBanlist(requestedDate));
            repo.Setup(r => r.GetAvailableListsAsync(token)).ReturnsAsync((IReadOnlyList<DateOnly>)[requestedDate]);
            var service = new DeckLegalityService(repo.Object);

            var result = await service.GetAsync(BuildDeck([]), DeckLegalityView.Current, eventID: null, listDate: requestedDate, token);

            Assert.IsNotNull(result.CardStatuses);
            repo.Verify(r => r.GetCurrentAsync(token), Times.Once);
        }

        [TestMethod]
        public async Task GetAsync_UnknownEventID_FallsBackToMostRecentEvent()
        {
            var recent = new Event { ID = 2, Date = new DateOnly(2024, 6, 1), Location = "Recent" };
            var older = new Event { ID = 1, Date = new DateOnly(2024, 1, 1), Location = "Older" };
            var deck = BuildDeck([recent, older]);
            var list = BuildBanlist(new DateOnly(2024, 5, 1));
            var repo = new Mock<IBanlistRepository>();
            repo.Setup(r => r.GetListForDateAsync(recent.Date, It.IsAny<CancellationToken>())).ReturnsAsync(list);
            repo.Setup(r => r.GetAvailableListsAsync(It.IsAny<CancellationToken>())).ReturnsAsync((IReadOnlyList<DateOnly>)[list.EffectiveDate]);
            var service = new DeckLegalityService(repo.Object);

            var result = await service.GetAsync(deck, DeckLegalityView.AtEvent, eventID: 999, listDate: null);

            Assert.AreEqual(recent.ID, result.AtEventSource!.ID);
        }

        private static Banlist BuildBanlist(DateOnly effectiveDate, BanlistLimit? restrictedCardLimit = null) =>
            new()
            {
                EffectiveDate = effectiveDate,
                LimitsByKonamiID = restrictedCardLimit is { } limit
                    ? new Dictionary<int, BanlistLimit> { [RESTRICTED_KONAMI_ID] = limit }
                    : new Dictionary<int, BanlistLimit>()
            };

        private static DeckDetailViewModel BuildDeck(IReadOnlyList<Event> events)
        {
            var empty = new DeckSectionViewModel { Cards = [] };
            var restrictedCard = new Card { ID = RESTRICTED_CARD_ID, KonamiID = RESTRICTED_KONAMI_ID, Name = "Test Restricted Card" };
            return new DeckDetailViewModel
            {
                Deck = new Deck { ID = 9, Name = "Test Deck" },
                Events = events,
                Extra = empty,
                Main = new DeckSectionViewModel { Cards = [new DeckCardViewModel { Card = restrictedCard, CardID = RESTRICTED_CARD_ID, Quantity = 1 }] },
                MainTypes = new DeckTypeCounts(0, 0, 0),
                Side = empty
            };
        }
    }
}
