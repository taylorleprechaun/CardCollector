using CardCollector.Data;
using CardCollector.DTO.YGOProDeck;
using Newtonsoft.Json;
using System.Diagnostics.CodeAnalysis;

namespace CardCollector.Repository.Sources
{
    public sealed class YGOProDeckSource : IYGOProDeckSource
    {
        private const string CACHE_FILE_NAME = "cardcache.json";

        /// <summary>
        /// Relative path of YGOProDeck's full card list endpoint.
        /// </summary>
        /// <remarks>
        /// misc=yes adds each card's Konami ID and pre-release beta id, which link it to yaml-yugi without its passcode.
        /// </remarks>
        private const string CARD_INFO_API_PATH = "api/v7/cardinfo.php?misc=yes";

        private readonly TimeSpan _cacheTTL;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<YGOProDeckSource> _logger;

        public YGOProDeckSource(ILogger<YGOProDeckSource> logger, IHttpClientFactory httpClientFactory, IConfiguration config)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _cacheTTL = TimeSpan.FromDays(config.GetValue<int>("CardDataSettings:ImageCacheTtlDays", 30));
        }

        /// <summary>True when at least one card carries a misc_info block; false means the cache predates misc=yes.</summary>
        public static bool HasMiscInfo(IEnumerable<YGOProDeckCard> cards) => cards.Any(c => c.MiscInfo is not null);

        /// <summary>
        /// Parses a cardinfo.php response into cards, copying each card's Konami ID out of its misc_info block onto
        /// <see cref="DTO.Card.KonamiID"/> (public for direct unit testing). Malformed JSON yields an empty list.
        /// </summary>
        public static IReadOnlyList<YGOProDeckCard> ParseCards(string json, ILogger? logger = null)
        {
            try
            {
                var cards = JsonConvert.DeserializeObject<CardInfoResponse>(json)?.Data ?? [];
                foreach (var card in cards)
                    card.KonamiID ??= card.MiscInfo?.FirstOrDefault()?.KonamiID;

                return cards;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to parse YGOProDeck card data");
                return [];
            }
        }

        [ExcludeFromCodeCoverage(Justification = "Cache-freshness check plus HTTP fetch and disk persistence; I/O, not testable logic.")]
        public async Task<IReadOnlyList<YGOProDeckCard>> LoadAsync(bool forceRefresh)
        {
            var (cacheDir, cachePath, timestampPath) = GetCachePaths();

            if (!forceRefresh && FileCacheHelper.IsCacheFresh(cachePath, timestampPath, _cacheTTL))
            {
                var cached = ReadCached();
                if (cached.Count == 0 || HasMiscInfo(cached))
                {
                    _logger.LogInformation("Loading YGOProDeck card data from cache ({Path})", cachePath);
                    return cached;
                }

                _logger.LogInformation("YGOProDeck card cache predates Konami IDs — fetching a fresh copy");
            }
            else
            {
                _logger.LogInformation("YGOProDeck card cache is missing or stale — fetching from YGOProDeck API");
            }

            var json = await FetchAsync().ConfigureAwait(false);
            if (json is not null)
            {
                Directory.CreateDirectory(cacheDir);
                File.WriteAllText(cachePath, json);
                FileCacheHelper.WriteTimestamp(timestampPath);
                _logger.LogInformation("YGOProDeck card data cached to {Path}", cachePath);
                return ParseCards(json, _logger);
            }

            if (File.Exists(cachePath))
                _logger.LogWarning("YGOProDeck fetch failed — falling back to stale card cache");
            else
                _logger.LogError("No YGOProDeck card data available — fetch failed and no cache exists");

            return ReadCached();
        }

        [ExcludeFromCodeCoverage(Justification = "Reads the cache file from disk; I/O, not testable logic.")]
        public IReadOnlyList<YGOProDeckCard> ReadCached()
        {
            var (_, cachePath, _) = GetCachePaths();
            if (File.Exists(cachePath))
                return ParseCards(File.ReadAllText(cachePath), _logger);

            _logger.LogWarning("No YGOProDeck card cache available — card images will use fallback URLs");
            return [];
        }

        [ExcludeFromCodeCoverage(Justification = "Builds well-known on-disk cache paths; trivial, not testable logic.")]
        private static (string CacheDir, string CachePath, string TimestampPath) GetCachePaths()
        {
            var cacheDir = Path.Combine(Directory.GetCurrentDirectory(), "Data");
            var cachePath = Path.Combine(cacheDir, CACHE_FILE_NAME);
            return (cacheDir, cachePath, cachePath + ".timestamp");
        }

        [ExcludeFromCodeCoverage(Justification = "Single HTTP GET; I/O, not testable logic.")]
        private async Task<string?> FetchAsync()
        {
            try
            {
                var client = _httpClientFactory.CreateClient("YGOProDeck");
                return await client.GetStringAsync(CARD_INFO_API_PATH).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch card data from YGOProDeck API");
                return null;
            }
        }
        private sealed class CardInfoResponse
        {
            [JsonProperty("data")]
            public List<YGOProDeckCard>? Data { get; set; }
        }
    }
}
