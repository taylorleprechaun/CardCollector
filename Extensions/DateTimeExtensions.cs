using System.Globalization;

namespace CardCollector.Extensions
{
    public static class DateTimeExtensions
    {
        /// <summary>
        /// "today", "yesterday", or "N days ago" within the last week, else "Oct 18, 2026". Both values are UTC and compared by
        /// calendar day, so a time later today still reads "today".
        /// </summary>
        public static string ToRelativeDate(this DateTime utc, DateTime nowUTC)
        {
            var days = (nowUTC.Date - utc.Date).Days;
            return days switch
            {
                0 => "today",
                1 => "yesterday",
                > 1 and < 7 => $"{days} days ago",
                _ => utc.ToString("MMM d, yyyy", CultureInfo.InvariantCulture)
            };
        }
    }
}
