namespace CardCollector.Data
{
    internal static class FileCacheHelper
    {
        public static bool IsCacheFresh(string cachePath, string timestampPath, TimeSpan ttl)
        {
            if (!File.Exists(cachePath) || !File.Exists(timestampPath))
                return false;

            var raw = File.ReadAllText(timestampPath);
            if (!DateTime.TryParse(raw, null, System.Globalization.DateTimeStyles.RoundtripKind, out var cachedAt))
                return false;

            return DateTime.UtcNow - cachedAt < ttl;
        }

        public static void TryDeleteFile(string path)
        {
            try { File.Delete(path); } catch { /* ignore */ }
        }

        public static void WriteTimestamp(string path) =>
            File.WriteAllText(path, DateTime.UtcNow.ToString("O"));
    }
}
