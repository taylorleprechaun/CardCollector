using System.Globalization;

namespace CardCollector.ViewModels
{
    /// <summary>Formats analytics numbers for display. A missing value shows as an em dash.</summary>
    public static class AnalyticsDisplay
    {
        public const string MISSING = "—";

        /// <summary>A 0–1 ratio as a percentage with one decimal place, e.g. <c>64.1%</c>.</summary>
        public static string Percent(double? ratio) =>
            ratio is { } value ? (value * 100).ToString("0.0", CultureInfo.InvariantCulture) + "%" : MISSING;

        /// <summary>The ratio as a plain number for sorting; missing values sort below every real value.</summary>
        public static string SortValue(double? value) =>
            value is { } number ? number.ToString("R", CultureInfo.InvariantCulture) : "-1";
    }
}
