using System.Text.Json;
using CardCollector.Data.Models;
using CardCollector.Services;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CardCollector.Pages.Tournaments
{
    public sealed class AnalyticsModel : PageModel
    {
        private static readonly JsonSerializerOptions TrendJsonOptions = new(JsonSerializerDefaults.Web);

        private readonly IAnalyticsService _analyticsService;
        private readonly IEventService _eventService;
        private readonly IFormatService _formatService;
        private readonly IMatchService _matchService;

        public AnalyticsModel(IAnalyticsService analyticsService, IEventService eventService, IFormatService formatService, IMatchService matchService)
        {
            _analyticsService = analyticsService;
            _eventService = eventService;
            _formatService = formatService;
            _matchService = matchService;
        }

        [BindProperty(SupportsGet = true)]
        public DateOnly? DateFrom { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateOnly? DateTo { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Deck { get; set; }

        public IReadOnlyList<string> DeckNames { get; private set; } = [];

        [BindProperty(SupportsGet = true)]
        public bool ExcludeByes { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? FormatID { get; set; }

        public IReadOnlyList<Format> Formats { get; private set; } = [];

        public bool HasActiveFilters =>
            HasSecondaryFilters
            || !string.IsNullOrWhiteSpace(Deck)
            || FormatID is not null
            || !string.IsNullOrWhiteSpace(Opponent);

        /// <summary>True when a filter under "More filters" is set, so that section starts open.</summary>
        public bool HasSecondaryFilters =>
            DateFrom is not null
            || DateTo is not null
            || ExcludeByes
            || !string.IsNullOrWhiteSpace(Location)
            || Type is not null;

        [BindProperty(SupportsGet = true)]
        public string? Location { get; set; }

        [BindProperty(SupportsGet = true)]
        public int MinMatches { get; set; } = AnalyticsCriteria.DEFAULT_MIN_MATCHES;

        [BindProperty(SupportsGet = true)]
        public string? Opponent { get; set; }

        public IReadOnlyList<string> OpponentNames { get; private set; } = [];

        public AnalyticsReport Report { get; private set; } = new();

        /// <summary>The by-format rows as JSON for the trend chart: label, events and match win rate.</summary>
        public string TrendJson { get; private set; } = "[]";

        [BindProperty(SupportsGet = true)]
        public EventType? Type { get; set; }

        public async Task OnGetAsync(CancellationToken cancellationToken)
        {
            if (MinMatches < 1)
                MinMatches = 1;

            Formats = await _formatService.GetAllAsync(cancellationToken).ConfigureAwait(false);
            DeckNames = await _eventService.GetDeckNamesAsync(cancellationToken).ConfigureAwait(false);
            OpponentNames = await _matchService.GetOpponentDecksAsync(cancellationToken).ConfigureAwait(false);

            Report = await _analyticsService.GetReportAsync(BuildCriteria(), cancellationToken).ConfigureAwait(false);
            TrendJson = JsonSerializer.Serialize(
                Report.ByFormat.Select(row => new { row.Label, Events = row.EventCount, row.Record.WinRate }),
                TrendJsonOptions);
        }

        private AnalyticsCriteria BuildCriteria() =>
            new()
            {
                DateFrom = DateFrom,
                DateTo = DateTo,
                DeckName = Deck,
                EventType = Type,
                ExcludeByes = ExcludeByes,
                FormatID = FormatID,
                Location = Location,
                MinMatches = MinMatches,
                Opponent = Opponent
            };
    }
}
