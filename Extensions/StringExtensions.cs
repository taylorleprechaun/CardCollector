namespace CardCollector.Extensions
{
    public static class StringExtensions
    {
        /// <summary>Returns the text trimmed, or null when it is null, empty or only whitespace.</summary>
        public static string? TrimToNull(this string? value)
        {
            var trimmed = value?.Trim();
            return string.IsNullOrEmpty(trimmed) ? null : trimmed;
        }
    }
}
