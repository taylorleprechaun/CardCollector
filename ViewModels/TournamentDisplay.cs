using System.Globalization;
using CardCollector.Data.Models;

namespace CardCollector.ViewModels
{
    /// <summary>Formats Tournaments dates and numbers for display. A missing value shows as an em dash.</summary>
    public static class TournamentDisplay
    {
        public const string MISSING = "—";

        /// <summary>A date as the pages show it, e.g. <c>Jul 8, 2021</c>.</summary>
        public static string Date(DateOnly date) =>
            date.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);

        /// <summary>A format's date range, e.g. <c>Jul 8, 2021 – Aug 11, 2021</c>; an open-ended format ends "Ongoing".</summary>
        public static string DateRange(Format format)
        {
            if (format is null) throw new ArgumentNullException(nameof(format));

            return $"{Date(format.StartDate)} – {(format.EndDate is { } end ? Date(end) : "Ongoing")}";
        }

        /// <summary>A date as <c>yyyy-MM-dd</c> for form values and data attributes; null when there is no date.</summary>
        public static string? IsoDate(DateOnly? date) =>
            date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        /// <summary>A 0–1 ratio as a percentage with one decimal place, e.g. <c>64.1%</c>.</summary>
        public static string Percent(double? ratio) =>
            ratio is { } value ? (value * 100).ToString("0.0", CultureInfo.InvariantCulture) + "%" : MISSING;

        /// <summary>The ratio as a plain number for sorting; missing values sort below every real value.</summary>
        public static string SortValue(double? value) =>
            value is { } number ? number.ToString("R", CultureInfo.InvariantCulture) : "-1";
    }
}
