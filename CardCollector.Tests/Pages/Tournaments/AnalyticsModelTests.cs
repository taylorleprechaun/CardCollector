using System.Text.Json;
using CardCollector.Data.Models;
using CardCollector.Pages.Tournaments;
using CardCollector.Services;
using CardCollector.Tests.TestHelpers;
using CardCollector.ViewModels;
using Moq;

namespace CardCollector.Tests.Pages.Tournaments
{
    [TestClass]
    public sealed class AnalyticsModelTests
    {
        [TestMethod]
        public void HasActiveFilters_NoFilters_IsFalse()
        {
            var (model, _) = CreateModel();

            Assert.IsFalse(model.HasActiveFilters);
            Assert.IsFalse(model.HasSecondaryFilters);
        }

        [TestMethod]
        [DataRow("deck", DisplayName = "Deck")]
        [DataRow("format", DisplayName = "Format")]
        [DataRow("opponent", DisplayName = "Opponent")]
        public void HasActiveFilters_PrimaryFilterSet_IsTrueButSecondaryIsFalse(string filter)
        {
            var (model, _) = CreateModel();
            switch (filter)
            {
                case "deck": model.Deck = "Sample"; break;
                case "format": model.FormatID = 3; break;
                default: model.Opponent = "Rival"; break;
            }

            Assert.IsTrue(model.HasActiveFilters);
            Assert.IsFalse(model.HasSecondaryFilters);
        }

        [TestMethod]
        public void HasActiveFilters_WhitespaceText_IsFalse()
        {
            var (model, _) = CreateModel();
            model.Deck = "   ";
            model.Location = " ";
            model.Opponent = "  ";

            Assert.IsFalse(model.HasActiveFilters);
        }

        [TestMethod]
        [DataRow("dateFrom", DisplayName = "Date from")]
        [DataRow("dateTo", DisplayName = "Date to")]
        [DataRow("excludeByes", DisplayName = "Exclude byes")]
        [DataRow("location", DisplayName = "Location")]
        [DataRow("type", DisplayName = "Type")]
        public void HasSecondaryFilters_SecondaryFilterSet_IsTrueAndCountsAsActive(string filter)
        {
            var (model, _) = CreateModel();
            switch (filter)
            {
                case "dateFrom": model.DateFrom = new DateOnly(2024, 1, 1); break;
                case "dateTo": model.DateTo = new DateOnly(2024, 1, 1); break;
                case "excludeByes": model.ExcludeByes = true; break;
                case "location": model.Location = "Test"; break;
                default: model.Type = EventType.YCS; break;
            }

            Assert.IsTrue(model.HasSecondaryFilters);
            Assert.IsTrue(model.HasActiveFilters);
        }

        [TestMethod]
        public async Task OnGetAsync_Always_LoadsFilterOptions()
        {
            var formats = new List<Format> { new() { ID = 1, Name = "Sample Format", StartDate = new DateOnly(2024, 1, 1) } };
            var (model, _) = CreateModel(formats, ["Sample Deck"], ["Sample Opponent"]);

            await model.OnGetAsync(CancellationToken.None);

            Assert.AreEqual(1, model.Formats.Count);
            CollectionAssert.AreEqual(new[] { "Sample Deck" }, model.DeckNames.ToArray());
            CollectionAssert.AreEqual(new[] { "Sample Opponent" }, model.OpponentNames.ToArray());
        }

        [TestMethod]
        public async Task OnGetAsync_BoundFilters_PassesThemToTheService()
        {
            var (model, analytics) = CreateModel();
            AnalyticsCriteria? captured = null;
            analytics.Setup(s => s.GetReportAsync(It.IsAny<AnalyticsCriteria>(), It.IsAny<CancellationToken>()))
                .Callback<AnalyticsCriteria, CancellationToken>((criteria, _) => captured = criteria)
                .ReturnsAsync(new AnalyticsReport());
            model.DateFrom = new DateOnly(2024, 1, 1);
            model.DateTo = new DateOnly(2024, 6, 30);
            model.Deck = "Sample";
            model.ExcludeByes = true;
            model.FormatID = 7;
            model.Location = "Hobby";
            model.MinMatches = 3;
            model.Opponent = "Rival";
            model.Type = EventType.Regional;

            await model.OnGetAsync(CancellationToken.None);

            Assert.IsNotNull(captured);
            Assert.AreEqual(new DateOnly(2024, 1, 1), captured.DateFrom);
            Assert.AreEqual(new DateOnly(2024, 6, 30), captured.DateTo);
            Assert.AreEqual("Sample", captured.DeckName);
            Assert.IsTrue(captured.ExcludeByes);
            Assert.AreEqual(7, captured.FormatID);
            Assert.AreEqual("Hobby", captured.Location);
            Assert.AreEqual(3, captured.MinMatches);
            Assert.AreEqual("Rival", captured.Opponent);
            Assert.AreEqual(EventType.Regional, captured.EventType);
        }

        [TestMethod]
        public async Task OnGetAsync_EmptyReport_LeavesAnEmptyTrend()
        {
            var (model, _) = CreateModel();

            await model.OnGetAsync(CancellationToken.None);

            Assert.IsTrue(model.Report.IsEmpty);
            Assert.AreEqual("[]", model.TrendJson);
        }

        [TestMethod]
        [DataRow(0, DisplayName = "Zero")]
        [DataRow(-4, DisplayName = "Negative")]
        public async Task OnGetAsync_MinMatchesBelowOne_UsesOne(int minMatches)
        {
            var (model, analytics) = CreateModel();
            AnalyticsCriteria? captured = null;
            analytics.Setup(s => s.GetReportAsync(It.IsAny<AnalyticsCriteria>(), It.IsAny<CancellationToken>()))
                .Callback<AnalyticsCriteria, CancellationToken>((criteria, _) => captured = criteria)
                .ReturnsAsync(new AnalyticsReport());
            model.MinMatches = minMatches;

            await model.OnGetAsync(CancellationToken.None);

            Assert.AreEqual(1, model.MinMatches);
            Assert.AreEqual(1, captured!.MinMatches);
        }

        [TestMethod]
        public async Task OnGetAsync_NoFilters_SendsDefaultCriteria()
        {
            var (model, analytics) = CreateModel();
            AnalyticsCriteria? captured = null;
            analytics.Setup(s => s.GetReportAsync(It.IsAny<AnalyticsCriteria>(), It.IsAny<CancellationToken>()))
                .Callback<AnalyticsCriteria, CancellationToken>((criteria, _) => captured = criteria)
                .ReturnsAsync(new AnalyticsReport());

            await model.OnGetAsync(CancellationToken.None);

            Assert.IsNull(captured!.DateFrom);
            Assert.IsNull(captured.DeckName);
            Assert.IsNull(captured.FormatID);
            Assert.IsFalse(captured.ExcludeByes);
            Assert.AreEqual(AnalyticsCriteria.DEFAULT_MIN_MATCHES, captured.MinMatches);
        }

        [TestMethod]
        public async Task OnGetAsync_ReportWithFormats_SerializesTheTrendInFormatOrder()
        {
            var (model, analytics) = CreateModel();
            analytics.Setup(s => s.GetReportAsync(It.IsAny<AnalyticsCriteria>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AnalyticsReport
                {
                    ByFormat =
                    [
                        new AnalyticsRecordRow("Early Format", 2, new WinLossTie(3, 1, 0)),
                        new AnalyticsRecordRow("Late Format", 1, new WinLossTie(0, 0, 0))
                    ]
                });

            await model.OnGetAsync(CancellationToken.None);

            using var trend = JsonDocument.Parse(model.TrendJson);
            var points = trend.RootElement.EnumerateArray().ToList();
            Assert.AreEqual(2, points.Count);
            Assert.AreEqual("Early Format", points[0].GetProperty("label").GetString());
            Assert.AreEqual(2, points[0].GetProperty("events").GetInt32());
            Assert.AreEqual(0.75, points[0].GetProperty("winRate").GetDouble());
            Assert.AreEqual(JsonValueKind.Null, points[1].GetProperty("winRate").ValueKind);
        }

        private static (AnalyticsModel Model, Mock<IAnalyticsService> Analytics) CreateModel(
            IReadOnlyList<Format>? formatList = null,
            IReadOnlyList<string>? deckNames = null,
            IReadOnlyList<string>? opponentNames = null)
        {
            var analytics = new Mock<IAnalyticsService>();
            analytics.Setup(s => s.GetReportAsync(It.IsAny<AnalyticsCriteria>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AnalyticsReport());

            var events = new Mock<IEventService>();
            events.Setup(s => s.GetDeckNamesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(deckNames ?? []);

            var formats = new Mock<IFormatService>();
            formats.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(formatList ?? []);

            var matches = new Mock<IMatchService>();
            matches.Setup(s => s.GetOpponentDecksAsync(It.IsAny<CancellationToken>())).ReturnsAsync(opponentNames ?? []);

            var model = new AnalyticsModel(analytics.Object, events.Object, formats.Object, matches.Object);
            PageContextFactory.Attach(model);
            return (model, analytics);
        }
    }
}
