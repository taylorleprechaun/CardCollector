namespace CardCollector.Extensions
{
    public static class CountExtensions
    {
        /// <summary>Returns the count with its noun, adding "s" unless the count is 1 ("1 card", "3 cards").</summary>
        public static string ToCountLabel(this int count, string noun)
        {
            if (noun is null) throw new ArgumentNullException(nameof(noun));

            return $"{count} {noun}{(count == 1 ? string.Empty : "s")}";
        }
    }
}
