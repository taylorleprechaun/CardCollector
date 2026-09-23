using CardCollector.Data.Models;
using CardCollector.Repository;
using CardCollector.Services;
using CardCollector.ViewModels;
using Moq;

namespace CardCollector.Tests.Services
{
    [TestClass]
    public sealed class DeckLegalityServiceTests
    {
        [TestMethod]
        public async Task GetAsync_AtEventViewForcedWithNoEvents_ReturnsNoActiveLegality()
        {
            var deck = BuildDeck([]);
            var repo = new Mock<IBanlistRepository>();
            repo.Setup(r => r.GetAvailableListsAsync()).ReturnsAsync((IReadOnlyList<DateOnly>)[new DateOnly(2024, 1, 1)]);
            var service = new DeckLegalityService(repo.Object);

            var result = await service.GetAsync(deck, DeckLegalityView.AtEvent, eventID: null, listDate: null);

            Assert.IsNull(result.ActiveLegality);
            Assert.IsNull(result.AtEventSource);
            repo.Verify(r => r.GetListForDateAsync(It.IsAny<DateOnly>()), Times.Never);
        }

        [TestMethod]
        public async Task GetAsync_DeckWithNoEvents_DefaultsToCurrentView()
        {
            var deck = BuildDeck([]);
            var current = BuildBanlist(new DateOnly(2026, 1, 1));
            var repo = new Mock<IBanlistRepository>();
            repo.Setup(r => r.GetCurrentAsync()).ReturnsAsync(current);
            repo.Setup(r => r.GetAvailableListsAsync()).ReturnsAsync((IReadOnlyList<DateOnly>)[current.EffectiveDate]);
            var service = new DeckLegalityService(repo.Object);

            var result = await service.GetAsync(deck, view: null, eventID: null, listDate: null);

            Assert.AreEqual(DeckLegalityView.Current, result.ActiveView);
            Assert.IsNull(result.AtEventSource);
        }

        [TestMethod]
        public async Task GetAsync_EventDateBeforeFirstList_ActiveLegalityIsNull()
        {
            var oldEvent = new Event { ID = 1, Date = new DateOnly(1990, 1, 1), Location = "Ancient" };
            var deck = BuildDeck([oldEvent]);
            var repo = new Mock<IBanlistRepository>();
            repo.Setup(r => r.GetListForDateAsync(oldEvent.Date)).ReturnsAsync((Banlist?)null);
            repo.Setup(r => r.GetAvailableListsAsync()).ReturnsAsync((IReadOnlyList<DateOnly>)[new DateOnly(2000, 1, 1)]);
            var service = new DeckLegalityService(repo.Object);

            var result = await service.GetAsync(deck, DeckLegalityView.AtEvent, eventID: null, listDate: null);

            Assert.IsTrue(result.IsAvailable);
            Assert.IsNull(result.ActiveLegality);
        }

        [TestMethod]
        public async Task GetAsync_EventDateResolvesCorrectList()
        {
            var tournamentEvent = new Event { ID = 1, Date = new DateOnly(2024, 6, 1), Location = "Locals" };
            var deck = BuildDeck([tournamentEvent]);
            var list = BuildBanlist(new DateOnly(2024, 5, 1));
            var repo = new Mock<IBanlistRepository>();
            repo.Setup(r => r.GetListForDateAsync(tournamentEvent.Date)).ReturnsAsync(list);
            repo.Setup(r => r.GetAvailableListsAsync()).ReturnsAsync((IReadOnlyList<DateOnly>)[list.EffectiveDate]);
            var service = new DeckLegalityService(repo.Object);

            var result = await service.GetAsync(deck, view: null, eventID: null, listDate: null);

            Assert.AreEqual(DeckLegalityView.AtEvent, result.ActiveView);
            Assert.AreEqual(list.EffectiveDate, result.ActiveLegality!.EffectiveDate);
        }

        [TestMethod]
        public async Task GetAsync_EventIDMatchesEvent_UsesThatEventRatherThanMostRecent()
        {
            var recent = new Event { ID = 2, Date = new DateOnly(2024, 6, 1), Location = "Recent" };
            var older = new Event { ID = 1, Date = new DateOnly(2024, 1, 1), Location = "Older" };
            var deck = BuildDeck([recent, older]);
            var list = BuildBanlist(new DateOnly(2023, 12, 1));
            var repo = new Mock<IBanlistRepository>();
            repo.Setup(r => r.GetListForDateAsync(older.Date)).ReturnsAsync(list);
            repo.Setup(r => r.GetAvailableListsAsync()).ReturnsAsync((IReadOnlyList<DateOnly>)[list.EffectiveDate]);
            var service = new DeckLegalityService(repo.Object);

            var result = await service.GetAsync(deck, DeckLegalityView.AtEvent, eventID: older.ID, listDate: null);

            Assert.AreEqual(older.ID, result.AtEventSource!.ID);
        }

        [TestMethod]
        public async Task GetAsync_ListDateOverrideOnAtEventView_UsesRequestedList()
        {
            var tournamentEvent = new Event { ID = 1, Date = new DateOnly(2024, 6, 1), Location = "Locals" };
            var deck = BuildDeck([tournamentEvent]);
            var overrideDate = new DateOnly(2022, 5, 17);
            var overrideList = BuildBanlist(overrideDate);
            var repo = new Mock<IBanlistRepository>();
            repo.Setup(r => r.GetListAsync(overrideDate)).ReturnsAsync(overrideList);
            repo.Setup(r => r.GetAvailableListsAsync()).ReturnsAsync((IReadOnlyList<DateOnly>)[overrideDate]);
            var service = new DeckLegalityService(repo.Object);

            var result = await service.GetAsync(deck, DeckLegalityView.AtEvent, eventID: null, listDate: overrideDate);

            Assert.AreEqual(overrideDate, result.ActiveLegality!.EffectiveDate);
            repo.Verify(r => r.GetListForDateAsync(It.IsAny<DateOnly>()), Times.Never);
        }

        [TestMethod]
        public async Task GetAsync_ListDateOverride_UsesRequestedListInsteadOfResolved()
        {
            var deck = BuildDeck([]);
            var overrideDate = new DateOnly(2022, 5, 17);
            var overrideList = BuildBanlist(overrideDate);
            var repo = new Mock<IBanlistRepository>();
            repo.Setup(r => r.GetListAsync(overrideDate)).ReturnsAsync(overrideList);
            repo.Setup(r => r.GetAvailableListsAsync()).ReturnsAsync((IReadOnlyList<DateOnly>)[overrideDate]);
            var service = new DeckLegalityService(repo.Object);

            var result = await service.GetAsync(deck, DeckLegalityView.Current, eventID: null, listDate: overrideDate);

            Assert.AreEqual(overrideDate, result.ActiveLegality!.EffectiveDate);
            repo.Verify(r => r.GetCurrentAsync(), Times.Once);
            repo.Verify(r => r.GetListAsync(overrideDate), Times.Once);
        }

        [TestMethod]
        public async Task GetAsync_ListDateRequested_IsEchoedOnViewModel()
        {
            var deck = BuildDeck([]);
            var requestedDate = new DateOnly(2022, 5, 17);
            var repo = new Mock<IBanlistRepository>();
            repo.Setup(r => r.GetListAsync(requestedDate)).ReturnsAsync(BuildBanlist(requestedDate));
            repo.Setup(r => r.GetAvailableListsAsync()).ReturnsAsync((IReadOnlyList<DateOnly>)[requestedDate]);
            var service = new DeckLegalityService(repo.Object);

            var result = await service.GetAsync(deck, DeckLegalityView.Current, eventID: null, listDate: requestedDate);

            Assert.AreEqual(requestedDate, result.RequestedListDate);
        }

        [TestMethod]
        public async Task GetAsync_NoBanlistData_ReportsUnavailable()
        {
            var deck = BuildDeck([]);
            var repo = new Mock<IBanlistRepository>();
            repo.Setup(r => r.GetCurrentAsync()).ReturnsAsync((Banlist?)null);
            repo.Setup(r => r.GetAvailableListsAsync()).ReturnsAsync((IReadOnlyList<DateOnly>)[]);
            var service = new DeckLegalityService(repo.Object);

            var result = await service.GetAsync(deck, view: null, eventID: null, listDate: null);

            Assert.IsFalse(result.IsAvailable);
            Assert.IsNull(result.ActiveLegality);
        }

        [TestMethod]
        public async Task GetAsync_NoListDateRequested_ViewModelRequestedListDateIsNull()
        {
            var deck = BuildDeck([]);
            var current = BuildBanlist(new DateOnly(2026, 1, 1));
            var repo = new Mock<IBanlistRepository>();
            repo.Setup(r => r.GetCurrentAsync()).ReturnsAsync(current);
            repo.Setup(r => r.GetAvailableListsAsync()).ReturnsAsync((IReadOnlyList<DateOnly>)[current.EffectiveDate]);
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
        public async Task GetAsync_UnknownEventID_FallsBackToMostRecentEvent()
        {
            var recent = new Event { ID = 2, Date = new DateOnly(2024, 6, 1), Location = "Recent" };
            var older = new Event { ID = 1, Date = new DateOnly(2024, 1, 1), Location = "Older" };
            var deck = BuildDeck([recent, older]);
            var list = BuildBanlist(new DateOnly(2024, 5, 1));
            var repo = new Mock<IBanlistRepository>();
            repo.Setup(r => r.GetListForDateAsync(recent.Date)).ReturnsAsync(list);
            repo.Setup(r => r.GetAvailableListsAsync()).ReturnsAsync((IReadOnlyList<DateOnly>)[list.EffectiveDate]);
            var service = new DeckLegalityService(repo.Object);

            var result = await service.GetAsync(deck, DeckLegalityView.AtEvent, eventID: 999, listDate: null);

            Assert.AreEqual(recent.ID, result.AtEventSource!.ID);
        }

        private static Banlist BuildBanlist(DateOnly effectiveDate) =>
            new() { EffectiveDate = effectiveDate, LimitsByKonamiID = new Dictionary<int, BanlistLimit>() };

        private static DeckDetailViewModel BuildDeck(IReadOnlyList<Event> events)
        {
            var empty = new DeckSectionViewModel { Cards = [] };
            return new DeckDetailViewModel
            {
                Deck = new Deck { ID = 9, Name = "Test Deck" },
                Events = events,
                Extra = empty,
                Main = empty,
                MainTypes = new DeckTypeCounts(0, 0, 0),
                Side = empty
            };
        }
    }
}
