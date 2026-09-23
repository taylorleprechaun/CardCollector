using CardCollector.Data;
using CardCollector.Data.Models;
using CardCollector.Repository;
using CardCollector.Rules;
using CardCollector.Services;
using CardCollector.Tests.TestHelpers;
using CardCollector.ViewModels;

namespace CardCollector.Tests.Services
{
    [TestClass]
    public sealed class AnalyticsServiceTests
    {
        [TestMethod]
        public async Task GetReportAsync_CombinedFilters_ReportsOnlyTheMatchingEvents()
        {
            using var context = InMemoryDbContextFactory.Create();
            var formatID = await SeedAsync(context);
            var service = CreateService(context);

            var report = await service.GetReportAsync(new AnalyticsCriteria { DeckName = "alpha", FormatID = formatID, Opponent = "rival" });

            Assert.AreEqual(1, report.Summary.EventCount);
            Assert.AreEqual(new WinLossTie(0, 1, 0), report.Summary.MatchRecord);
        }

        [TestMethod]
        [DataRow("ALPHA DECK", 2, DisplayName = "Deck name in capitals")]
        [DataRow("beta", 1, DisplayName = "Partial deck name in lower case")]
        public async Task GetReportAsync_DeckNameFilter_MatchesContainsIgnoringCase(string deckName, int expectedEvents)
        {
            using var context = InMemoryDbContextFactory.Create();
            await SeedAsync(context);
            var service = CreateService(context);

            var report = await service.GetReportAsync(new AnalyticsCriteria { DeckName = deckName });

            Assert.AreEqual(expectedEvents, report.Summary.EventCount);
        }

        [TestMethod]
        public async Task GetReportAsync_FormatFilter_UsesTheFormatDerivedFromEachEventsDate()
        {
            using var context = InMemoryDbContextFactory.Create();
            var formatID = await SeedAsync(context);
            var service = CreateService(context);

            var report = await service.GetReportAsync(new AnalyticsCriteria { FormatID = formatID });

            Assert.AreEqual(2, report.Summary.EventCount);
            Assert.IsTrue(report.DeckFormats.All(r => r.FormatName == "Sample Format"));
        }

        [TestMethod]
        public async Task GetReportAsync_NoCriteria_CountsEventsWithoutRoundsAndOutsideEveryFormat()
        {
            using var context = InMemoryDbContextFactory.Create();
            await SeedAsync(context);
            var service = CreateService(context);

            var report = await service.GetReportAsync(new AnalyticsCriteria());

            Assert.AreEqual(3, report.Summary.EventCount);
            Assert.AreEqual(new WinLossTie(2, 1, 0), report.Summary.MatchRecord);
            Assert.AreEqual(FormatRules.NO_FORMAT_NAME, report.ByFormat[^1].Label);
        }

        [TestMethod]
        public async Task GetReportAsync_NoEvents_ReturnsAnEmptyReport()
        {
            using var context = InMemoryDbContextFactory.Create();
            var service = CreateService(context);

            var report = await service.GetReportAsync(new AnalyticsCriteria());

            Assert.IsTrue(report.IsEmpty);
        }

        [TestMethod]
        public async Task GetReportAsync_NullCriteria_ThrowsArgumentNullException()
        {
            using var context = InMemoryDbContextFactory.Create();
            var service = CreateService(context);

            await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => service.GetReportAsync(null!));
        }

        private static AnalyticsService CreateService(AppDBContext context) =>
            new(new EventRepository(context), new FormatService(new FormatRepository(context)));

        /// <summary>
        /// Two "Alpha Deck" events inside the format (one with a bye and a loss to a rival, one with no rounds)
        /// and one "Beta Deck" event before it. Returns the format's ID.
        /// </summary>
        private static async Task<int> SeedAsync(AppDBContext context)
        {
            var format = new Format { Name = "Sample Format", StartDate = new DateOnly(2024, 1, 1), EndDate = new DateOnly(2024, 6, 30) };
            context.Formats.Add(format);

            var withRounds = new Event { Date = new DateOnly(2024, 2, 1), DeckName = "Alpha Deck", Location = "Test Hobby Shop" };
            var withoutRounds = new Event { Date = new DateOnly(2024, 3, 1), DeckName = "alpha deck", Location = "Test Hobby Shop" };
            var beforeFormat = new Event { Date = new DateOnly(2023, 6, 1), DeckName = "Beta Deck", Location = "Test Hobby Shop" };
            context.Events.AddRange(withRounds, withoutRounds, beforeFormat);
            await context.SaveChangesAsync();

            context.Matches.AddRange(
                new Match { EventID = withRounds.ID, IsBye = true, OpponentDeck = "Bye", Result = MatchResult.Win, Round = "1", Sequence = 1 },
                new Match { EventID = withRounds.ID, OpponentDeck = "Rival Deck", Result = MatchResult.Loss, Round = "2", Sequence = 2 },
                new Match { EventID = beforeFormat.ID, OpponentDeck = "Sample Opponent", Result = MatchResult.Win, Round = "1", Sequence = 1 });
            await context.SaveChangesAsync();

            return format.ID;
        }
    }
}
