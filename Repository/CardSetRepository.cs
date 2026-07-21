using System.Diagnostics.CodeAnalysis;
using CardCollector.DTO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace CardCollector.Repository
{
    public sealed class CardSetRepository : ICardSetRepository
    {
        private readonly int _cacheTtlDays;
        private readonly IReadOnlyDictionary<string, string> _dateByCode;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<CardSetRepository> _logger;

        [ExcludeFromCodeCoverage(Justification = "Loads cached/live set data from disk and HTTP on construction; I/O orchestration, not testable logic.")]
        public CardSetRepository(ILogger<CardSetRepository> logger, IHttpClientFactory httpClientFactory, IConfiguration config)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _cacheTtlDays = config.GetValue<int>("CardDataSettings:CacheTtlDays", 7);

            var sets = LoadSets();
            _dateByCode = BuildDateIndex(sets);
        }

        // Public for direct unit testing — pure data transformation, no I/O.
        public static IReadOnlyDictionary<string, string> BuildDateIndex(IReadOnlyList<CardSetData> sets)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var set in sets)
            {
                if (string.IsNullOrEmpty(set.Code) || string.IsNullOrEmpty(set.TCGDate))
                    continue;

                if (!dict.TryGetValue(set.Code, out var existing) ||
                    string.Compare(set.TCGDate, existing, StringComparison.Ordinal) < 0)
                {
                    dict[set.Code] = set.TCGDate;
                }
            }
            return dict;
        }

        // Public for direct unit testing — pure JSON parsing with fallback, no I/O. logger is optional
        // so tests can exercise the malformed-JSON path without needing a real ILogger.
        public static IReadOnlyList<CardSetData> Deserialize(string json, ILogger? logger = null)
        {
            try
            {
                return JsonConvert.DeserializeObject<List<CardSetData>>(json) ?? [];
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to deserialize card set data");
                return [];
            }
        }

        public string? GetTCGDateBySetCode(string fullSetCode) => GetTCGDateBySetCode(_dateByCode, fullSetCode);

        // Public for direct unit testing — pure prefix-lookup logic, no I/O.
        public static string? GetTCGDateBySetCode(IReadOnlyDictionary<string, string> dateByCode, string fullSetCode)
        {
            if (string.IsNullOrEmpty(fullSetCode))
                return null;

            var prefix = fullSetCode.Split('-')[0];
            return dateByCode.GetValueOrDefault(prefix);
        }

        [ExcludeFromCodeCoverage(Justification = "Reads set-cache JSON from disk; I/O, not testable logic.")]
        private IReadOnlyList<CardSetData> DeserializeFromFile(string path)
        {
            var json = File.ReadAllText(path);
            return Deserialize(json, _logger);
        }

        [ExcludeFromCodeCoverage(Justification = "HTTP fetch of card set data; I/O, not testable logic.")]
        private async Task<string?> FetchFromAPIAsync()
        {
            try
            {
                var client = _httpClientFactory.CreateClient("YGOProDeck");
                return await client.GetStringAsync("api/v7/cardsets.php").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch card set data from YGOProDeck API");
                return null;
            }
        }

        [ExcludeFromCodeCoverage(Justification = "Checks cache-file timestamps on disk; I/O, not testable logic.")]
        private bool IsCacheFresh(string cachePath, string timestampPath)
        {
            if (!File.Exists(cachePath) || !File.Exists(timestampPath))
                return false;

            var raw = File.ReadAllText(timestampPath);
            if (!DateTime.TryParse(raw, null, System.Globalization.DateTimeStyles.RoundtripKind, out var cachedAt))
                return false;

            return DateTime.UtcNow - cachedAt < TimeSpan.FromDays(_cacheTtlDays);
        }

        [ExcludeFromCodeCoverage(Justification = "Cache-freshness check plus file/HTTP fallback orchestration; I/O, not testable logic.")]
        private IReadOnlyList<CardSetData> LoadSets()
        {
            var cacheDir = Path.Combine(Directory.GetCurrentDirectory(), "Data");
            var cachePath = Path.Combine(cacheDir, "setscache.json");
            var timestampPath = cachePath + ".timestamp";

            if (IsCacheFresh(cachePath, timestampPath))
            {
                _logger.LogInformation("Loading card set data from cache ({Path})", cachePath);
                return DeserializeFromFile(cachePath);
            }

            _logger.LogInformation("Set cache is missing or stale — fetching from YGOProDeck API");
            var json = Task.Run(FetchFromAPIAsync).GetAwaiter().GetResult();

            if (json is not null)
            {
                Directory.CreateDirectory(cacheDir);
                File.WriteAllText(cachePath, json);
                File.WriteAllText(timestampPath, DateTime.UtcNow.ToString("O"));
                _logger.LogInformation("Card set data cached to {Path}", cachePath);
                return Deserialize(json, _logger);
            }

            if (File.Exists(cachePath))
            {
                _logger.LogWarning("API fetch failed — falling back to stale set cache");
                return DeserializeFromFile(cachePath);
            }

            _logger.LogError("No card set data available — API fetch failed and no cache exists");
            return [];
        }
    }
}
