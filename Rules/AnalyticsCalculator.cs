using CardCollector.Data.Models;
using CardCollector.Extensions;
using CardCollector.Models;
using CardCollector.ViewModels;

namespace CardCollector.Rules
{
    /// <summary>
    /// Pure filtering and tallying for tournament analytics. Every record counts each round's stored result,
    /// never one worked out from the game score.
    /// </summary>
    public static class AnalyticsCalculator
    {
        public const string DICE_LOST_LABEL = "Lost the roll";
        public const string DICE_NOT_RECORDED_LABEL = "Not recorded";
        public const string DICE_WON_LABEL = "Won the roll";
        public const string TOTAL_LABEL = "Total";

        /// <summary>Works out the report for events that have already been filtered.</summary>
        /// <param name="events">The filtered events.</param>
        /// <param name="minMatches">The fewest rounds against an opponent for it to be listed; values below 1 count as 1.</param>
        public static AnalyticsReport Calculate(IReadOnlyList<AnalyticsEvent> events, int minMatches)
        {
            if (events is null) throw new ArgumentNullException(nameof(events));

            var matches = events.SelectMany(e => e.Matches).ToList();
            var matchRecord = EventRules.GetMatchRecord(matches);

            return new AnalyticsReport
            {
                ByDeck = GetByDeck(events),
                ByEventType = GetByEventType(events),
                ByFormat = GetByFormat(events),
                DeckFormatTotal = new AnalyticsRecordRow(TOTAL_LABEL, events.Count, matchRecord),
                DeckFormats = GetDeckFormats(events),
                Dice = GetDice(events),
                Matchups = GetMatchups(events, Math.Max(1, minMatches)),
                Summary = GetSummary(events, matches, matchRecord)
            };
        }

        /// <summary>
        /// Resolves each event's format from its date and applies the criteria. Byes are dropped when
        /// <see cref="AnalyticsCriteria.ExcludeByes"/> is set, and an opponent filter keeps only the matching rounds
        /// and the events that have at least one.
        /// </summary>
        public static IReadOnlyList<AnalyticsEvent> Filter(IEnumerable<Event> events, IReadOnlyList<Format> formats, AnalyticsCriteria criteria)
        {
            if (events is null) throw new ArgumentNullException(nameof(events));
            if (formats is null) throw new ArgumentNullException(nameof(formats));
            if (criteria is null) throw new ArgumentNullException(nameof(criteria));

            var deckName = criteria.DeckName.TrimToNull();
            var location = criteria.Location.TrimToNull();
            var opponent = criteria.Opponent.TrimToNull();
            var results = new List<AnalyticsEvent>();

            foreach (var tournamentEvent in events)
            {
                if (!MatchesEventFilters(tournamentEvent, criteria, deckName, location))
                    continue;

                var format = FormatRules.FindForDate(formats, tournamentEvent.Date);
                if (criteria.FormatID is { } formatID && format?.ID != formatID)
                    continue;

                var matches = tournamentEvent.Matches
                    .Where(m => !criteria.ExcludeByes || !m.IsBye)
                    .Where(m => opponent is null || Contains(m.OpponentDeck, opponent))
                    .ToList();

                if (opponent is not null && matches.Count == 0)
                    continue;

                results.Add(new AnalyticsEvent(tournamentEvent, format, matches));
            }

            return results;
        }

        private static bool Contains(string? value, string text) =>
            value is not null && value.Contains(text, StringComparison.OrdinalIgnoreCase);

        private static AnalyticsRecordRow CreateEventRow(string label, IEnumerable<AnalyticsEvent> events)
        {
            var list = events.ToList();
            return new AnalyticsRecordRow(label, list.Count, EventRules.GetMatchRecord(list.SelectMany(e => e.Matches)));
        }

        /// <summary>Groups the non-bye rounds; each row's event count is the number of distinct events its rounds came from.</summary>
        private static IReadOnlyList<AnalyticsRecordRow> CreateRoundRows(
            IReadOnlyList<AnalyticsEvent> events,
            Func<Match, string> labelSelector,
            IEqualityComparer<string> comparer)
        {
            return events
                .SelectMany(e => e.Matches.Where(m => !m.IsBye).Select(m => (EventID: e.Event.ID, Match: m)))
                .GroupBy(round => labelSelector(round.Match), comparer)
                .Select(group => new AnalyticsRecordRow(
                    labelSelector(group.First().Match),
                    group.Select(round => round.EventID).Distinct().Count(),
                    EventRules.GetMatchRecord(group.Select(round => round.Match))))
                .ToList();
        }

        private static string DiceLabel(bool? wonDiceRoll) => wonDiceRoll switch
        {
            true => DICE_WON_LABEL,
            false => DICE_LOST_LABEL,
            null => DICE_NOT_RECORDED_LABEL
        };

        private static IReadOnlyList<AnalyticsRecordRow> GetByDeck(IReadOnlyList<AnalyticsEvent> events) =>
            events
                .GroupBy(e => TrimText(e.Event.DeckName), StringComparer.OrdinalIgnoreCase)
                .Select(group => CreateEventRow(TrimText(group.First().Event.DeckName), group))
                .OrderByDescending(row => row.EventCount)
                .ThenBy(row => row.Label, StringComparer.OrdinalIgnoreCase)
                .ToList();

        private static IReadOnlyList<AnalyticsRecordRow> GetByEventType(IReadOnlyList<AnalyticsEvent> events) =>
            events
                .GroupBy(e => e.Event.EventType)
                .Select(group => CreateEventRow(group.First().Event.EventType.GetDisplayName(), group))
                .OrderByDescending(row => row.EventCount)
                .ThenBy(row => row.Label, StringComparer.OrdinalIgnoreCase)
                .ToList();

        private static IReadOnlyList<AnalyticsRecordRow> GetByFormat(IReadOnlyList<AnalyticsEvent> events) =>
            events
                .GroupBy(e => e.Format?.ID)
                .Select(group => (Format: group.First().Format, Row: CreateEventRow(group.First().Format?.Name ?? FormatRules.NO_FORMAT_NAME, group)))
                .OrderBy(item => item.Format is null)
                .ThenBy(item => item.Format?.StartDate)
                .Select(item => item.Row)
                .ToList();

        private static IReadOnlyList<DeckFormatRow> GetDeckFormats(IReadOnlyList<AnalyticsEvent> events) =>
            events
                .GroupBy(e => (Deck: TrimText(e.Event.DeckName).ToUpperInvariant(), FormatID: e.Format?.ID))
                .Select(group =>
                {
                    var format = group.First().Format;
                    var row = CreateEventRow(TrimText(group.First().Event.DeckName), group);
                    return new DeckFormatRow(row.Label, format?.Name ?? FormatRules.NO_FORMAT_NAME, format?.StartDate, row.EventCount, row.Record);
                })
                .OrderBy(row => row.FormatStartDate is null)
                .ThenByDescending(row => row.FormatStartDate)
                .ThenBy(row => row.DeckName, StringComparer.OrdinalIgnoreCase)
                .ToList();

        private static IReadOnlyList<AnalyticsRecordRow> GetDice(IReadOnlyList<AnalyticsEvent> events)
        {
            var rows = CreateRoundRows(events, m => DiceLabel(m.WonDiceRoll), StringComparer.Ordinal);
            string[] order = [DICE_WON_LABEL, DICE_LOST_LABEL, DICE_NOT_RECORDED_LABEL];

            return order
                .Select(label => rows.FirstOrDefault(row => row.Label == label) ?? new AnalyticsRecordRow(label, 0, new WinLossTie(0, 0, 0)))
                .ToList();
        }

        private static IReadOnlyList<AnalyticsRecordRow> GetMatchups(IReadOnlyList<AnalyticsEvent> events, int minMatches) =>
            CreateRoundRows(events, m => TrimText(m.OpponentDeck), StringComparer.OrdinalIgnoreCase)
                .Where(row => row.Record.Total >= minMatches)
                .OrderByDescending(row => row.Record.Total)
                .ThenBy(row => row.Label, StringComparer.OrdinalIgnoreCase)
                .ToList();

        private static AnalyticsSummary GetSummary(IReadOnlyList<AnalyticsEvent> events, IReadOnlyList<Match> matches, WinLossTie matchRecord) =>
            new()
            {
                EventCount = events.Count,
                FirstPlaceCount = events.Count(e => e.Event.Finish == 1),
                GameRecord = EventRules.GetGameRecord(matches),
                MatchRecord = matchRecord
            };

        private static bool MatchesEventFilters(Event tournamentEvent, AnalyticsCriteria criteria, string? deckName, string? location) =>
            (criteria.DateFrom is not { } from || tournamentEvent.Date >= from)
            && (criteria.DateTo is not { } to || tournamentEvent.Date <= to)
            && (criteria.EventType is not { } eventType || tournamentEvent.EventType == eventType)
            && (deckName is null || Contains(tournamentEvent.DeckName, deckName))
            && (location is null || Contains(tournamentEvent.Location, location));

        private static string TrimText(string? value) => value?.Trim() ?? string.Empty;
    }
}
