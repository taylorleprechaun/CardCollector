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

        private readonly IReadOnlyList<Card> _browseableCards;
        private readonly int _cacheTtlDays;
        private readonly IReadOnlyDictionary<int, Card> _cardIndex;
        private readonly IReadOnlyList<Card> _cards;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly int _imageCacheTtlDays;
        private readonly ILogger<CardDataRepository> _logger;
        private readonly IReadOnlyDictionary<string, string> _setNamesByCode;
        private readonly string _yamlYugiUrl;

        public IReadOnlyList<string> DistinctAttributes { get; }
        public IReadOnlyList<string> DistinctRarityNames { get; }
        public IReadOnlyList<string> DistinctSetNames { get; }

        public CardDataRepository(ILogger<CardDataRepository> logger, IHttpClientFactory httpClientFactory, IConfiguration config)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _cacheTtlDays = config.GetValue<int>("CardDataSettings:CacheTtlDays", 7);
            _imageCacheTtlDays = config.GetValue<int>("CardDataSettings:ImageCacheTtlDays", 30);
            _yamlYugiUrl = config.GetValue<string>("CardDataSettings:YamlYugiUrl")
                ?? "https://dawnbrandbots.github.io/yaml-yugi/cards.yaml";

            var rawCards = LoadCards();
            var imagesByCardID = LoadImages();
            AttachImages(rawCards, imagesByCardID);

            _cards = rawCards;
            _browseableCards = rawCards.Where(c => c.CardSets?.Any() == true).ToList();
            _cardIndex = rawCards.ToDictionary(c => c.ID);
            _setNamesByCode = BuildSetNameIndex(rawCards);
            DistinctAttributes = _browseableCards
                .Where(c => !string.IsNullOrEmpty(c.Attribute))
                .Select(c => c.Attribute!)
                .Distinct()
                .OrderBy(a => a)
                .ToList();
            DistinctRarityNames = _browseableCards
                .SelectMany(c => c.CardSets ?? [])
                .Select(s => s.RarityName)
                .Where(r => !string.IsNullOrEmpty(r))
                .Select(r => r!)
                .Distinct()
                .OrderBy(r => r)
                .ToList();
            DistinctSetNames = _browseableCards
                .SelectMany(c => c.CardSets ?? [])
                .Select(s => s.Name)
                .Where(n => !string.IsNullOrEmpty(n))
                .Select(n => n!)
                .Distinct()
                .OrderBy(n => n)
                .ToList();
        }

        public IEnumerable<Card> GetAllCards() => _cards;

        public IEnumerable<Card> GetBrowseableCards() => _browseableCards;

        public Card? GetCardByID(int cardID) =>
            _cardIndex.GetValueOrDefault(cardID);

        public IReadOnlyDictionary<string, string> GetSetNamesByCode() => _setNamesByCode;

        private static void AttachImages(IReadOnlyList<Card> cards, IReadOnlyDictionary<int, IReadOnlyList<Image>> imagesByCardID)
        {
            foreach (var card in cards)
            {
                if (imagesByCardID.TryGetValue(card.ID, out var images))
                    card.CardImages = images;
                else
                    card.CardImages = [BuildFallbackImage(card.ID)];
            }
        }

        private static Image BuildFallbackImage(int cardID) => new()
        {
            ID = cardID,
            ImageURL = $"https://images.ygoprodeck.com/images/cards/{cardID}.jpg",
            ImageURLSmall = $"https://images.ygoprodeck.com/images/cards_small/{cardID}.jpg"
        };

        private static IReadOnlyDictionary<string, string> BuildSetNameIndex(IReadOnlyList<Card> cards)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var card in cards)
            {
                foreach (var set in card.CardSets ?? [])
                {
                    if (!string.IsNullOrEmpty(set.Code) && !string.IsNullOrEmpty(set.Name))
                        dict.TryAdd(set.Code, set.Name);
                }
            }
            return dict;
        }

        private static IReadOnlyList<Set> BuildSets(YamlCard y)
        {
            var sets = new List<Set>();
            foreach (var entry in y.Sets?.En ?? [])
            {
                if (entry.SetName?.Contains("Speed Duel", StringComparison.OrdinalIgnoreCase) == true)
                    continue;

                foreach (var rarity in entry.Rarities ?? [])
                {
                    sets.Add(new Set
                    {
                        Code = entry.SetNumber,
                        Name = entry.SetName,
                        RarityName = rarity,
                        RarityCode = RarityExtensions.GetRarityCode(rarity),
                    });
                }
            }
            return sets;
        }

        private static IReadOnlyList<Card> ConvertYamlCards(IReadOnlyList<YamlCard> yamlCards)
        {
            var result = new List<Card>(yamlCards.Count);
            foreach (var y in yamlCards)
            {
                if (y.Password is not int id)
                    continue;

                var sets = BuildSets(y);
                result.Add(new Card
                {
                    ID = id,
                    Name = y.Name?.En,
                    Description = y.Text?.En,
                    Attribute = y.Attribute,
                    Level = y.Level ?? y.Rank,
                    ATK = int.TryParse(y.Atk, out var atk) ? atk : null,
                    DEF = int.TryParse(y.Def, out var def) ? def : null,
                    LinkRating = y.LinkArrows?.Count,
                    CardType = DeriveCardType(y),
                    Type = DeriveRace(y),
                    CardSets = sets.Count > 0 ? sets : null,
                });
            }
            return result;
        }

        private static string DeriveCardType(YamlCard y) => y.CardType switch
        {
            "Spell" => "Spell Card",
            "Trap" => "Trap Card",
            _ => DeriveMonsterType(y.MonsterTypeLine)
        };

        private static string DeriveMonsterType(string? typeLine)
        {
            if (typeLine is null) return "Normal Monster";
            if (typeLine.Contains("Fusion")) return "Fusion Monster";
            if (typeLine.Contains("Synchro")) return "Synchro Monster";
            if (typeLine.Contains("Xyz")) return "Xyz Monster";
            if (typeLine.Contains("Link")) return "Link Monster";
            if (typeLine.Contains("Ritual")) return "Ritual Monster";
            if (typeLine.Contains("Effect")) return "Effect Monster";
            return "Normal Monster";
        }

        private static string? DeriveRace(YamlCard y)
        {
            if (y.MonsterTypeLine is not null)
                return y.MonsterTypeLine.Split(" / ").First();
            return y.Property;
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

        private static bool IsCacheFresh(string cachePath, string timestampPath, int ttlDays)
        {
            if (!File.Exists(cachePath) || !File.Exists(timestampPath))
                return false;

            var raw = File.ReadAllText(timestampPath);
            if (!DateTime.TryParse(raw, null, System.Globalization.DateTimeStyles.RoundtripKind, out var cachedAt))
                return false;

            return DateTime.UtcNow - cachedAt < TimeSpan.FromDays(ttlDays);
        }

        private IReadOnlyList<Card> LoadCards()
        {
            var cacheDir = Path.Combine(Directory.GetCurrentDirectory(), "Data");
            var cardDataPath = Path.Combine(cacheDir, "carddata.json");
            var timestampPath = cardDataPath + ".timestamp";

            if (IsCacheFresh(cardDataPath, timestampPath, _cacheTtlDays))
            {
                _logger.LogInformation("Loading card data from cache ({Path})", cardDataPath);
                return LoadCardsFromJson(cardDataPath);
            }

            _logger.LogInformation("Card data cache is missing or stale — fetching from yaml-yugi");
            var yamlCards = Task.Run(FetchFromYamlYugiAsync).GetAwaiter().GetResult();

            if (yamlCards is not null)
            {
                var cards = ConvertYamlCards(yamlCards);
                Directory.CreateDirectory(cacheDir);
                File.WriteAllText(cardDataPath, JsonConvert.SerializeObject(cards));
                File.WriteAllText(timestampPath, DateTime.UtcNow.ToString("O"));
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

            if (IsCacheFresh(cachePath, timestampPath, _imageCacheTtlDays))
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
                    File.WriteAllText(timestampPath, DateTime.UtcNow.ToString("O"));
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

        private static void SkipToNextDocument(IParser parser)
        {
            while (parser.Current is not null and not DocumentEnd and not StreamEnd)
                parser.MoveNext();
            if (parser.Current is DocumentEnd)
                parser.MoveNext();
        }

        private sealed class ImageCacheRoot
        {
            [JsonProperty("data")]
            public IEnumerable<ImageCacheCard>? Data { get; set; }
        }

        private sealed class ImageCacheCard
        {
            [JsonProperty("card_images")]
            public IEnumerable<Image>? CardImages { get; set; }

            [JsonProperty("id")]
            public int ID { get; set; }
        }
    }
}
