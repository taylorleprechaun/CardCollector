using CardCollector.Data;
using CardCollector.DTO;
using CardCollector.DTO.YamlYugi;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace CardCollector.Repository
{
    public sealed class CardDataRepository : ICardDataRepository
    {
        private const string CardInfoApiPath = "api/v7/cardinfo.php";

        private readonly int _cacheTtlDays;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly int _imageCacheTtlDays;
        private readonly ILogger<CardDataRepository> _logger;
        private readonly string _yamlYugiUrl;
        private IReadOnlyList<Card> _browseableCards = default!;
        private IReadOnlyDictionary<int, Card> _cardIndex = default!;
        private IReadOnlyList<Card> _cards = default!;
        private IReadOnlyDictionary<string, string> _setNamesByCode = default!;
        private IReadOnlyDictionary<string, string> _setPrefixByName = default!;
        public CardDataRepository(ILogger<CardDataRepository> logger, IHttpClientFactory httpClientFactory, IConfiguration config)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _cacheTtlDays = config.GetValue<int>("CardDataSettings:CacheTtlDays", 7);
            _imageCacheTtlDays = config.GetValue<int>("CardDataSettings:ImageCacheTtlDays", 30);
            _yamlYugiUrl = config.GetValue<string>("CardDataSettings:YamlYugiUrl")
                ?? "https://dawnbrandbots.github.io/yaml-yugi/cards.yaml";

            Initialize(LoadCards(), LoadImages());
        }

        public IReadOnlyList<string> DistinctRarityNames { get; private set; } = default!;
        public IReadOnlyList<string> DistinctSetNames { get; private set; } = default!;
        public IEnumerable<Card> GetAllCards() => _cards;

        public IEnumerable<Card> GetBrowseableCards() => _browseableCards;

        public Card? GetCardByID(int cardID) =>
            _cardIndex.GetValueOrDefault(cardID);

        public IReadOnlyDictionary<string, string> GetSetNamesByCode() => _setNamesByCode;

        public string? GetSetPrefixByName(string name) =>
            _setPrefixByName.TryGetValue(name, out var prefix) ? prefix : null;

        public async Task RefreshAsync()
        {
            var cacheDir = Path.Combine(Directory.GetCurrentDirectory(), "Data");
            FileCacheHelper.TryDeleteFile(Path.Combine(cacheDir, "carddata.json.timestamp"));
            FileCacheHelper.TryDeleteFile(Path.Combine(cacheDir, "cardcache.json.timestamp"));

            await Task.Run(() => Initialize(LoadCards(), LoadImages())).ConfigureAwait(false);
        }

        private static void SkipToNextDocument(IParser parser)
        {
            while (parser.Current is not null and not DocumentEnd and not StreamEnd)
                parser.MoveNext();
            if (parser.Current is DocumentEnd)
                parser.MoveNext();
        }

        private async Task<IReadOnlyList<YamlCard>?> FetchFromYamlYugiAsync()
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                _logger.LogInformation("Downloading yaml-yugi card data from {Url}", _yamlYugiUrl);
                var yaml = await client.GetStringAsync(_yamlYugiUrl).ConfigureAwait(false);
                return ParseYamlCards(yaml);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch card data from yaml-yugi");
                return null;
            }
        }

        private async Task<string?> FetchImageCacheAsync()
        {
            try
            {
                var client = _httpClientFactory.CreateClient("YGOProDeck");
                return await client.GetStringAsync(CardInfoApiPath).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch image cache from YGOProDeck API");
                return null;
            }
        }

        private void Initialize(IReadOnlyList<Card> rawCards, IReadOnlyDictionary<int, IReadOnlyList<Image>> imagesByCardID)
        {
            CardDataMapper.AttachImages(rawCards, imagesByCardID);

            _cards = rawCards;
            _browseableCards = rawCards.Where(c => c.CardSets?.Any() == true).ToList();
            _cardIndex = rawCards.ToDictionary(c => c.ID);

            var (setNamesByCode, setPrefixByName) = CardDataMapper.BuildSetNameIndex(rawCards);
            _setNamesByCode = setNamesByCode;
            _setPrefixByName = setPrefixByName;

            var browseablePrefixes = new HashSet<string>(
                _browseableCards
                    .SelectMany(c => c.CardSets ?? [])
                    .Where(s => !string.IsNullOrEmpty(s.Code))
                    .Select(s => CardDataMapper.GetSetPrefix(s.Code!)),
                StringComparer.OrdinalIgnoreCase);

            DistinctRarityNames = _browseableCards
                .SelectMany(c => c.CardSets ?? [])
                .Select(s => s.RarityName)
                .Where(r => !string.IsNullOrEmpty(r))
                .Select(r => r!)
                .Distinct()
                .OrderBy(r => r)
                .ToList();

            DistinctSetNames = _setPrefixByName
                .Where(kvp => browseablePrefixes.Contains(kvp.Value))
                .Select(kvp => kvp.Key)
                .OrderBy(n => n)
                .ToList();
        }

        private IReadOnlyList<Card> LoadCards()
        {
            var cacheDir = Path.Combine(Directory.GetCurrentDirectory(), "Data");
            var cardDataPath = Path.Combine(cacheDir, "carddata.json");
            var timestampPath = cardDataPath + ".timestamp";

            if (FileCacheHelper.IsCacheFresh(cardDataPath, timestampPath, TimeSpan.FromDays(_cacheTtlDays)))
            {
                _logger.LogInformation("Loading card data from cache ({Path})", cardDataPath);
                return LoadCardsFromJson(cardDataPath);
            }

            _logger.LogInformation("Card data cache is missing or stale — fetching from yaml-yugi");
            var yamlCards = Task.Run(FetchFromYamlYugiAsync).GetAwaiter().GetResult();

            if (yamlCards is not null)
            {
                var cards = CardDataMapper.ConvertYamlCards(yamlCards);
                Directory.CreateDirectory(cacheDir);
                File.WriteAllText(cardDataPath, JsonConvert.SerializeObject(cards));
                FileCacheHelper.WriteTimestamp(timestampPath);
                _logger.LogInformation("Card data cached to {Path} ({Count} cards)", cardDataPath, cards.Count);
                return cards;
            }

            if (File.Exists(cardDataPath))
            {
                _logger.LogWarning("yaml-yugi fetch failed — falling back to stale card data cache");
                return LoadCardsFromJson(cardDataPath);
            }

            _logger.LogError("No card data available — yaml-yugi fetch failed and no cache exists");
            return [];
        }

        private IReadOnlyList<Card> LoadCardsFromJson(string path)
        {
            try
            {
                var json = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<List<Card>>(json) ?? [];
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize card data from {Path}", path);
                return [];
            }
        }

        private IReadOnlyDictionary<int, IReadOnlyList<Image>> LoadImages()
        {
            var cacheDir = Path.Combine(Directory.GetCurrentDirectory(), "Data");
            var cachePath = Path.Combine(cacheDir, "cardcache.json");
            var timestampPath = cachePath + ".timestamp";

            if (FileCacheHelper.IsCacheFresh(cachePath, timestampPath, TimeSpan.FromDays(_imageCacheTtlDays)))
            {
                _logger.LogInformation("Loading image data from cache ({Path})", cachePath);
            }
            else
            {
                _logger.LogInformation("Image cache is missing or stale — fetching from YGOProDeck API");
                var json = Task.Run(FetchImageCacheAsync).GetAwaiter().GetResult();
                if (json is not null)
                {
                    Directory.CreateDirectory(cacheDir);
                    File.WriteAllText(cachePath, json);
                    FileCacheHelper.WriteTimestamp(timestampPath);
                    _logger.LogInformation("Image cache saved to {Path}", cachePath);
                }
                else if (File.Exists(cachePath))
                {
                    _logger.LogWarning("Image cache fetch failed — falling back to stale image cache");
                }
            }

            if (!File.Exists(cachePath))
            {
                _logger.LogWarning("No image cache available — card images will use fallback URLs");
                return new Dictionary<int, IReadOnlyList<Image>>();
            }

            try
            {
                var json = File.ReadAllText(cachePath);
                var root = JsonConvert.DeserializeObject<ImageCacheRoot>(json);
                return (root?.Data ?? [])
                    .Where(c => c.CardImages?.Any() == true)
                    .ToDictionary(c => c.ID, c => (IReadOnlyList<Image>)c.CardImages!.ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse image cache — card images will use fallback URLs");
                return new Dictionary<int, IReadOnlyList<Image>>();
            }
        }

        private IReadOnlyList<YamlCard> ParseYamlCards(string yaml)
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
                    _logger.LogWarning(ex, "Skipping unparseable yaml-yugi card document");
                    SkipToNextDocument(parser);
                }
            }

            _logger.LogInformation("Parsed {Count} card documents from yaml-yugi", cards.Count);
            return cards;
        }
        private sealed class ImageCacheCard
        {
            [JsonProperty("card_images")]
            public IEnumerable<Image>? CardImages { get; set; }

            [JsonProperty("id")]
            public int ID { get; set; }
        }

        private sealed class ImageCacheRoot
        {
            [JsonProperty("data")]
            public IEnumerable<ImageCacheCard>? Data { get; set; }
        }
    }
}
