namespace CardCollector.Extensions
{
    public static class StringExtensions
    {
        /// <summary>The escape character <see cref="ToContainsLikePattern"/> uses; pass it to <c>EF.Functions.Like</c>.</summary>
        public const string LIKE_ESCAPE = @"\";

        /// <summary>
        /// Returns a SQL LIKE pattern matching any text that contains <paramref name="value"/>, with its own <c>%</c>,
        /// <c>_</c>, and escape characters escaped by <see cref="LIKE_ESCAPE"/> so they match literally.
        /// </summary>
        public static string ToContainsLikePattern(this string value)
        {
            if (value is null) throw new ArgumentNullException(nameof(value));

            return $"%{EscapeLike(value)}%";
        }

        /// <summary>Returns a SQL LIKE pattern matching any text that starts with <paramref name="value"/>, escaped like <see cref="ToContainsLikePattern"/>.</summary>
        public static string ToStartsWithLikePattern(this string value)
        {
            if (value is null) throw new ArgumentNullException(nameof(value));

            return $"{EscapeLike(value)}%";
        }

        /// <summary>Returns the text trimmed, or null when it is null, empty or only whitespace.</summary>
        public static string? TrimToNull(this string? value)
        {
            var trimmed = value?.Trim();
            return string.IsNullOrEmpty(trimmed) ? null : trimmed;
        }

        private static string EscapeLike(string value) =>
            value.Replace(LIKE_ESCAPE, LIKE_ESCAPE + LIKE_ESCAPE, StringComparison.Ordinal)
                .Replace("%", LIKE_ESCAPE + "%", StringComparison.Ordinal)
                .Replace("_", LIKE_ESCAPE + "_", StringComparison.Ordinal);
    }
}
