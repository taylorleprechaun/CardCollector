using System.Globalization;
using CardCollector.Data.Models;

namespace CardCollector.Rules
{
    /// <summary>Pure validation, normalization and lookup rules for <see cref="Format"/>.</summary>
    public static class FormatRules
    {
        public const int MAX_NAME_LENGTH = 100;
        public const int MAX_STRATEGIES = 10;
        public const int MAX_STRATEGY_LENGTH = 100;
        public const string NO_FORMAT_NAME = "No format";

        /// <summary>
        /// Returns the format whose inclusive date range contains <paramref name="date"/>. An open-ended
        /// format (no end date) contains every later date. If ranges overlap, the latest start date wins.
        /// </summary>
        public static Format? FindForDate(IEnumerable<Format> formats, DateOnly date)
        {
            if (formats is null) throw new ArgumentNullException(nameof(formats));

            return formats
                .Where(f => f.StartDate <= date && (f.EndDate is null || date <= f.EndDate))
                .OrderByDescending(f => f.StartDate)
                .FirstOrDefault();
        }

        /// <summary>
        /// Returns a copy with the name and notes trimmed and the strategies trimmed, stripped of blanks and
        /// de-duplicated case-insensitively (first spelling wins), renumbered in order.
        /// </summary>
        public static Format Normalize(Format format)
        {
            if (format is null) throw new ArgumentNullException(nameof(format));

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var strategies = new List<FormatStrategy>();
            foreach (var name in format.Strategies.Select(s => s.Name?.Trim()))
            {
                if (string.IsNullOrEmpty(name) || !seen.Add(name))
                    continue;

                strategies.Add(new FormatStrategy { Name = name, Position = strategies.Count });
            }

            var notes = format.Notes?.Trim();

            return new Format
            {
                EndDate = format.EndDate,
                ID = format.ID,
                Name = format.Name?.Trim() ?? string.Empty,
                Notes = string.IsNullOrEmpty(notes) ? null : notes,
                StartDate = format.StartDate,
                Strategies = strategies
            };
        }

        /// <summary>
        /// Validates an already-normalized format against the other formats. The format being edited is
        /// excluded from the overlap check by ID.
        /// </summary>
        public static IReadOnlyList<string> Validate(Format format, IEnumerable<Format> existingFormats)
        {
            if (format is null) throw new ArgumentNullException(nameof(format));
            if (existingFormats is null) throw new ArgumentNullException(nameof(existingFormats));

            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(format.Name))
                errors.Add("Name is required.");
            else if (format.Name.Length > MAX_NAME_LENGTH)
                errors.Add($"Name must be {MAX_NAME_LENGTH} characters or fewer.");

            if (format.EndDate is not null && format.EndDate < format.StartDate)
                errors.Add("End date must be on or after the start date.");

            if (format.Strategies.Count() > MAX_STRATEGIES)
                errors.Add($"A format can have at most {MAX_STRATEGIES} strategies.");

            if (format.Strategies.Any(s => s.Name.Length > MAX_STRATEGY_LENGTH))
                errors.Add($"Each strategy must be {MAX_STRATEGY_LENGTH} characters or fewer.");

            foreach (var other in existingFormats.Where(f => f.ID != format.ID && Overlaps(format, f)))
                errors.Add($"Dates overlap with \"{other.Name}\" ({DescribeRange(other)}).");

            return errors;
        }

        private static string DescribeRange(Format format)
        {
            var start = format.StartDate.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
            var end = format.EndDate?.ToString("MMM d, yyyy", CultureInfo.InvariantCulture) ?? "Ongoing";
            return $"{start} – {end}";
        }

        private static bool Overlaps(Format a, Format b) =>
            (b.EndDate is null || a.StartDate <= b.EndDate) &&
            (a.EndDate is null || b.StartDate <= a.EndDate);
    }
}
