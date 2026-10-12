using CardCollector.Data;
using CardCollector.DTO.YamlYugi;
using Newtonsoft.Json;
using System.Diagnostics.CodeAnalysis;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace CardCollector.Repository.Sources
{
    public sealed class YamlYugiSource : IYamlYugiSource
    {
        private const string CACHE_FILE_NAME = "yamlyugi.json";

        private readonly TimeSpan _cacheTTL;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<YamlYugiSource> _logger;
        private readonly string _yamlYugiURL;

        public YamlYugiSource(ILogger<YamlYugiSource> logger, IHttpClientFactory httpClientFactory, IConfiguration config)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _cacheTTL = TimeSpan.FromDays(config.GetValue<int>("CardDataSettings:CacheTtlDays", 7));
            _yamlYugiURL = config.GetValue<string>("CardDataSettings:YamlYugiUrl") ?? "https://dawnbrandbots.github.io/yaml-yugi/cards.yaml";
        }

        /// <summary>
        /// Parses a yaml-yugi multi-document YAML stream into card documents, skipping any document that fails to
        /// parse (public for direct unit testing). Unmapped fields are ignored, since real documents carry many.
        /// </summary>
        public static IReadOnlyList<YamlCard> ParseYaml(string yaml, ILogger? logger = null)
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var cards = new List<YamlCard>();
            using var reader = new StringReader(yaml);
            var parser = new Parser(reader);
            parser.Consume<StreamStart>();

            while (parser.Accept<DocumentStart>(out _))
            {
                try
                {
                    var card = deserializer.Deserialize<YamlCard>(parser);
                    if (card is not null)
                        cards.Add(card);
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "Skipping unparseable yaml-yugi card document");
                    SkipToNextDocument(parser);
                }
            }

            logger?.LogInformation("Parsed {Count} card documents from yaml-yugi", cards.Count);
            return cards;
        }

        [ExcludeFromCodeCoverage(Justification = "Cache-freshness check plus HTTP fetch and disk persistence; I/O, not testable logic.")]
        public async Task<IReadOnlyList<YamlCard>> LoadAsync(bool forceRefresh)
        {
            var (cacheDir, cachePath, timestampPath) = GetCachePaths();

            if (!forceRefresh && FileCacheHelper.IsCacheFresh(cachePath, timestampPath, _cacheTTL))
            {
                _logger.LogInformation("Loading yaml-yugi card data from cache ({Path})", cachePath);
                return ReadCached();
            }

            _logger.LogInformation("Downloading yaml-yugi card data from {Url}", _yamlYugiURL);
            var cards = await FetchAsync().ConfigureAwait(false);

            if (cards is not null)
            {
                Directory.CreateDirectory(cacheDir);
                File.WriteAllText(cachePath, JsonConvert.SerializeObject(cards));
                FileCacheHelper.WriteTimestamp(timestampPath);
                _logger.LogInformation("yaml-yugi card data cached to {Path} ({Count} cards)", cachePath, cards.Count);
                return cards;
            }

            if (File.Exists(cachePath))
            {
                _logger.LogWarning("yaml-yugi fetch failed — falling back to stale card data cache");
                return ReadCached();
            }

            _logger.LogError("No yaml-yugi card data available — fetch failed and no cache exists");
            return [];
        }

        [ExcludeFromCodeCoverage(Justification = "Reads the cache file from disk; I/O, not testable logic.")]
        public IReadOnlyList<YamlCard> ReadCached()
        {
            var (_, cachePath, _) = GetCachePaths();
            if (!File.Exists(cachePath))
                return [];

            try
            {
                return JsonConvert.DeserializeObject<List<YamlCard>>(File.ReadAllText(cachePath)) ?? [];
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize yaml-yugi card cache from {Path}", cachePath);
                return [];
            }
        }

        [ExcludeFromCodeCoverage(Justification = "Builds well-known on-disk cache paths; trivial, not testable logic.")]
        private static (string CacheDir, string CachePath, string TimestampPath) GetCachePaths()
        {
            var cacheDir = Path.Combine(Directory.GetCurrentDirectory(), "Data");
            var cachePath = Path.Combine(cacheDir, CACHE_FILE_NAME);
            return (cacheDir, cachePath, cachePath + ".timestamp");
        }

        private static void SkipToNextDocument(IParser parser)
        {
            while (parser.Current is not null and not DocumentEnd and not StreamEnd)
                parser.MoveNext();
            if (parser.Current is DocumentEnd)
                parser.MoveNext();
        }

        [ExcludeFromCodeCoverage(Justification = "Single HTTP GET plus parse; I/O, not testable logic.")]
        private async Task<IReadOnlyList<YamlCard>?> FetchAsync()
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var yaml = await client.GetStringAsync(_yamlYugiURL).ConfigureAwait(false);
                return ParseYaml(yaml, _logger);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch card data from yaml-yugi");
                return null;
            }
        }
    }
}
