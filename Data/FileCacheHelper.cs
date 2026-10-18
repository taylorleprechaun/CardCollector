namespace CardCollector.Data
{
    internal static class FileCacheHelper
    {
        public static bool IsCacheFresh(string cachePath, string timestampPath, TimeSpan ttl) =>
            File.Exists(cachePath) && ReadTimestamp(timestampPath) is DateTime cachedAt && DateTime.UtcNow - cachedAt < ttl;

        /// <summary>Reads the UTC time a cache was written from its timestamp file; null when the file is missing or unreadable.</summary>
        public static DateTime? ReadTimestamp(string timestampPath)
        {
            if (!File.Exists(timestampPath))
                return null;

            var raw = File.ReadAllText(timestampPath);
            return DateTime.TryParse(raw, null, System.Globalization.DateTimeStyles.RoundtripKind, out var cachedAt)
                ? cachedAt.ToUniversalTime()
                : null;
        }

        public static void TryDeleteFile(string path)
        {
            try { File.Delete(path); } catch { /* ignore */ }
        }

        public static void WriteTimestamp(string path) =>
            File.WriteAllText(path, DateTime.UtcNow.ToString("O"));
    }
}
