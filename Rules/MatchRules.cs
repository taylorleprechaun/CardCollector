using System.Globalization;
using CardCollector.Data.Models;
using CardCollector.ViewModels;

namespace CardCollector.Rules
{
    /// <summary>Pure validation, normalization and suggestion rules for <see cref="Match"/>.</summary>
    public static class MatchRules
    {
        public const string BYE_OPPONENT_NAME = "Bye";
        public const int MAX_GAMES_WON_OR_LOST = 2;
        public const int MAX_OPPONENT_LENGTH = 150;
        public const int MAX_ROUND_LENGTH = 20;

        /// <summary>Top-cut round labels in the order they are played, after the numbered rounds.</summary>
        private static readonly string[] TopCutLabels = ["Top 8", "Top 4", "Finals"];

        /// <summary>
        /// Works out where a new round belongs among the existing ones: straight after the last existing round that
        /// is not later than it, with numbered rounds before top-cut rounds. A label that isn't a number or a
        /// known top-cut round goes at the end. Returns a zero-based index between 0 and the number of existing rounds.
        /// </summary>
        /// <param name="existingRounds">The existing round labels, in play order.</param>
        /// <param name="round">The label of the round being added.</param>
        public static int GetInsertPosition(IReadOnlyList<string> existingRounds, string round)
        {
            if (existingRounds is null) throw new ArgumentNullException(nameof(existingRounds));

            if (GetRoundKey(round) is not { } key)
                return existingRounds.Count;

            var position = 0;
            for (var index = 0; index < existingRounds.Count; index++)
            {
                if (GetRoundKey(existingRounds[index]) is { } existingKey && existingKey.CompareTo(key) <= 0)
                    position = index + 1;
            }

            return position;
        }

        /// <summary>
        /// True when the rounds are already in round order: numbered rounds, then Top 8, Top 4 and Finals.
        /// Any other label counts as coming after those.
        /// </summary>
        /// <param name="rounds">The round labels, in stored order.</param>
        public static bool IsInRoundOrder(IEnumerable<string> rounds)
        {
            if (rounds is null) throw new ArgumentNullException(nameof(rounds));

            var keys = rounds.Select(GetSortKey).ToList();
            return keys.Zip(keys.Skip(1), (earlier, later) => earlier.CompareTo(later) <= 0).All(inOrder => inOrder);
        }

        /// <summary>
        /// Suggests the label for the next round: the last numeric round plus one, or the next top-cut round.
        /// Returns an empty string when there is nothing sensible to suggest.
        /// </summary>
        /// <param name="rounds">The existing round labels, in play order.</param>
        public static string NextRoundLabel(IEnumerable<string> rounds)
        {
            if (rounds is null) throw new ArgumentNullException(nameof(rounds));

            var last = rounds.LastOrDefault()?.Trim();
            if (string.IsNullOrEmpty(last))
                return "1";

            if (int.TryParse(last, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
                return (number + 1).ToString(CultureInfo.InvariantCulture);

            var next = Array.FindIndex(TopCutLabels, label => label.Equals(last, StringComparison.OrdinalIgnoreCase)) + 1;
            return next > 0 && next < TopCutLabels.Length ? TopCutLabels[next] : string.Empty;
        }

        /// <summary>
        /// Returns a copy of the round's own fields with text trimmed and blank notes turned into null. A bye is
        /// forced to a win with no games and no dice roll, and a blank opponent becomes <see cref="BYE_OPPONENT_NAME"/>.
        /// </summary>
        public static Match Normalize(Match match)
        {
            if (match is null) throw new ArgumentNullException(nameof(match));

            var normalized = new Match
            {
                EventID = match.EventID,
                GamesLost = match.GamesLost,
                GamesTied = match.GamesTied,
                GamesWon = match.GamesWon,
                ID = match.ID,
                IsBye = match.IsBye,
                Notes = TrimToNull(match.Notes),
                OpponentDeck = match.OpponentDeck?.Trim() ?? string.Empty,
                Result = match.Result,
                Round = match.Round?.Trim() ?? string.Empty,
                Sequence = match.Sequence,
                WonDiceRoll = match.WonDiceRoll
            };

            if (!normalized.IsBye)
                return normalized;

            normalized.GamesLost = 0;
            normalized.GamesTied = 0;
            normalized.GamesWon = 0;
            normalized.Result = MatchResult.Win;
            normalized.WonDiceRoll = null;
            if (normalized.OpponentDeck.Length == 0)
                normalized.OpponentDeck = BYE_OPPONENT_NAME;

            return normalized;
        }

        /// <summary>
        /// Returns the rounds in round order (see <see cref="IsInRoundOrder"/>). Rounds with the same label keep
        /// their existing relative order.
        /// </summary>
        public static IReadOnlyList<Match> OrderByRound(IEnumerable<Match> matches)
        {
            if (matches is null) throw new ArgumentNullException(nameof(matches));

            return matches.OrderBy(m => GetSortKey(m.Round)).ToList();
        }

        /// <summary>
        /// Suggests a result from the game score. The stored result may differ, because a recorded result can be
        /// overridden by hand.
        /// </summary>
        public static MatchResult SuggestResult(int gamesWon, int gamesLost, int gamesTied)
        {
            var halfTies = gamesTied / 2.0;

            if (gamesWon > gamesLost + halfTies)
                return MatchResult.Win;

            return gamesLost > gamesWon + halfTies ? MatchResult.Loss : MatchResult.Tie;
        }

        /// <summary>Tallies a set of rounds and suggests the label for the round that would come next.</summary>
        public static MatchSummaryViewModel Summarize(IEnumerable<Match> matches)
        {
            if (matches is null) throw new ArgumentNullException(nameof(matches));

            var list = matches.ToList();
            return new MatchSummaryViewModel
            {
                DiceRecord = EventRules.GetDiceRecord(list),
                GameRecord = EventRules.GetGameRecord(list),
                MatchRecord = EventRules.GetMatchRecord(list),
                NextRound = NextRoundLabel(list.Select(m => m.Round)),
                RoundCount = list.Count
            };
        }

        /// <summary>Validates an already-normalized round.</summary>
        public static IReadOnlyList<string> Validate(Match match)
        {
            if (match is null) throw new ArgumentNullException(nameof(match));

            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(match.Round))
                errors.Add("Round is required.");
            else if (match.Round.Length > MAX_ROUND_LENGTH)
                errors.Add($"Round must be {MAX_ROUND_LENGTH} characters or fewer.");

            if (!match.IsBye && string.IsNullOrWhiteSpace(match.OpponentDeck))
                errors.Add("Opponent deck is required.");
            else if (match.OpponentDeck.Length > MAX_OPPONENT_LENGTH)
                errors.Add($"Opponent deck must be {MAX_OPPONENT_LENGTH} characters or fewer.");

            if (!IsValidWinOrLossCount(match.GamesWon) || !IsValidWinOrLossCount(match.GamesLost) || match.GamesTied < 0)
                errors.Add($"Games won and lost must each be between 0 and {MAX_GAMES_WON_OR_LOST}, and games tied can't be negative.");

            if (!Enum.IsDefined(match.Result))
                errors.Add("Result is not valid.");

            return errors;
        }

        /// <summary>Sort key for a round label: numbered rounds first, then the top-cut rounds. Null when the label is neither.</summary>
        private static (int Group, int Order)? GetRoundKey(string? round)
        {
            var trimmed = round?.Trim();
            if (string.IsNullOrEmpty(trimmed))
                return null;

            if (int.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
                return (0, number);

            var topCut = Array.FindIndex(TopCutLabels, label => label.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
            return topCut >= 0 ? (1, topCut) : null;
        }

        /// <summary>Sort key for a round label; a label that is neither a number nor a top-cut round sorts last.</summary>
        private static (int Group, int Order) GetSortKey(string? round) => GetRoundKey(round) ?? (2, 0);

        private static bool IsValidWinOrLossCount(int games) => games is >= 0 and <= MAX_GAMES_WON_OR_LOST;

        private static string? TrimToNull(string? value)
        {
            var trimmed = value?.Trim();
            return string.IsNullOrEmpty(trimmed) ? null : trimmed;
        }
    }
}
