using CardCollector.Data.Models;
using CardCollector.Extensions;
using CardCollector.ViewModels;

namespace CardCollector.Rules
{
    /// <summary>Pure validation, normalization and tally rules for <see cref="Event"/>.</summary>
    public static class EventRules
    {
        public const int MAX_DECK_NAME_LENGTH = 150;
        public const int MAX_LOCATION_LENGTH = 150;
        public const int MAX_URL_LENGTH = 500;

        /// <summary>Counts how often the dice roll was won and lost; rounds with no recorded roll are skipped.</summary>
        public static DiceRecord GetDiceRecord(IEnumerable<Match> matches)
        {
            if (matches is null) throw new ArgumentNullException(nameof(matches));

            var rolls = matches.Where(m => m.WonDiceRoll is not null).ToList();
            return new DiceRecord(rolls.Count(m => m.WonDiceRoll == true), rolls.Count(m => m.WonDiceRoll == false));
        }

        /// <summary>Sums the games won, lost and tied across the rounds.</summary>
        public static WinLossTie GetGameRecord(IEnumerable<Match> matches)
        {
            if (matches is null) throw new ArgumentNullException(nameof(matches));

            var list = matches.ToList();
            return new WinLossTie(list.Sum(m => m.GamesWon), list.Sum(m => m.GamesLost), list.Sum(m => m.GamesTied));
        }

        /// <summary>Counts the stored round results. A bye counts by its stored result, like any other round.</summary>
        public static WinLossTie GetMatchRecord(IEnumerable<Match> matches)
        {
            if (matches is null) throw new ArgumentNullException(nameof(matches));

            var list = matches.ToList();
            return new WinLossTie(
                list.Count(m => m.Result == MatchResult.Win),
                list.Count(m => m.Result == MatchResult.Loss),
                list.Count(m => m.Result == MatchResult.Tie));
        }

        /// <summary>
        /// Returns a copy of the event's own fields with text trimmed and blank optional text turned into null.
        /// The rounds and deck link are not copied.
        /// </summary>
        public static Event Normalize(Event tournamentEvent)
        {
            if (tournamentEvent is null) throw new ArgumentNullException(nameof(tournamentEvent));

            return new Event
            {
                Date = tournamentEvent.Date,
                DeckName = tournamentEvent.DeckName?.Trim() ?? string.Empty,
                DecklistURL = tournamentEvent.DecklistURL.TrimToNull(),
                EventType = tournamentEvent.EventType,
                Finish = tournamentEvent.Finish,
                FinishNote = tournamentEvent.FinishNote.TrimToNull(),
                ID = tournamentEvent.ID,
                Location = tournamentEvent.Location?.Trim() ?? string.Empty,
                Notes = tournamentEvent.Notes.TrimToNull(),
                Players = tournamentEvent.Players,
                TopCut = tournamentEvent.TopCut.TrimToNull()
            };
        }

        /// <summary>Validates an already-normalized event.</summary>
        public static IReadOnlyList<string> Validate(Event tournamentEvent)
        {
            if (tournamentEvent is null) throw new ArgumentNullException(nameof(tournamentEvent));

            var errors = new List<string>();

            if (tournamentEvent.Date == default)
                errors.Add("Date is required.");

            if (string.IsNullOrWhiteSpace(tournamentEvent.Location))
                errors.Add("Location is required.");
            else if (tournamentEvent.Location.Length > MAX_LOCATION_LENGTH)
                errors.Add($"Location must be {MAX_LOCATION_LENGTH} characters or fewer.");

            if (string.IsNullOrWhiteSpace(tournamentEvent.DeckName))
                errors.Add("Deck name is required.");
            else if (tournamentEvent.DeckName.Length > MAX_DECK_NAME_LENGTH)
                errors.Add($"Deck name must be {MAX_DECK_NAME_LENGTH} characters or fewer.");

            if (!Enum.IsDefined(tournamentEvent.EventType))
                errors.Add("Event type is not valid.");

            if (tournamentEvent.Players is < 1)
                errors.Add("Players must be at least 1.");

            if (tournamentEvent.Finish is < 1)
                errors.Add("Finish must be at least 1.");
            else if (tournamentEvent.Finish is { } finish && tournamentEvent.Players is { } players && finish > players)
                errors.Add("Finish can't be higher than the number of players.");

            if (tournamentEvent.DecklistURL is { } url)
            {
                if (url.Length > MAX_URL_LENGTH)
                    errors.Add($"Decklist URL must be {MAX_URL_LENGTH} characters or fewer.");
                else if (!IsHttpURL(url))
                    errors.Add("Decklist URL must be a full http or https address.");
            }

            return errors;
        }

        private static bool IsHttpURL(string value) =>
            Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
