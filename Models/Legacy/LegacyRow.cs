using System.Globalization;

namespace CardCollector.Models.Legacy
{
    /// <summary>
    /// One row read from a table of the legacy (pre-catalog) database, as column name → value. The typed getters read
    /// SQLite's storage forms: integers, reals, and text (dates are text, enums are their names).
    /// </summary>
    public sealed class LegacyRow
    {
        /// <summary>The raw column values, keyed case-insensitively by column name.</summary>
        public IReadOnlyDictionary<string, object?> Values { get; }

        public LegacyRow(IReadOnlyDictionary<string, object?> values)
        {
            Values = values ?? throw new ArgumentNullException(nameof(values));
        }

        public DateTime GetDateTime(string column) =>
            GetNullableDateTime(column) ?? throw new InvalidOperationException($"Legacy column {column} is null.");

        public decimal? GetDecimal(string column) =>
            Get(column) is { } value ? Convert.ToDecimal(value, CultureInfo.InvariantCulture) : null;

        public TEnum? GetEnum<TEnum>(string column) where TEnum : struct, Enum =>
            Get(column) is string text && Enum.TryParse<TEnum>(text, ignoreCase: true, out var parsed) ? parsed : null;

        public int GetInt(string column) =>
            GetNullableInt(column) ?? throw new InvalidOperationException($"Legacy column {column} is null.");

        public DateTime? GetNullableDateTime(string column) =>
            Get(column) is string text
                ? DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal)
                : null;

        public int? GetNullableInt(string column) =>
            Get(column) is { } value ? Convert.ToInt32(value, CultureInfo.InvariantCulture) : null;

        public string? GetString(string column) => Get(column) as string;

        private object? Get(string column) =>
            Values.TryGetValue(column, out var value) && value is not DBNull ? value : null;
    }
}
