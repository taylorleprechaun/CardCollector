using CardCollector.DTO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace CardCollector.Repository
{
    public sealed class CardDataRepository : ICardDataRepository
    {
        private readonly IReadOnlyList<(Card Card, Image Image)> _artworks;
        private readonly IReadOnlyList<(Card Card, Image Image)> _browseableArtworks;
        private readonly IReadOnlyList<Card> _browseableCards;
        private readonly int _cacheTtlDays;
        private readonly Dictionary<int, Card> _cardIndex;
        private readonly IReadOnlyList<Card> _cards;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<CardDataRepository> _logger;
        private readonly IReadOnlyDictionary<string, string> _setNamesByCode;

        public IReadOnlyList<string> DistinctAttributes { get; }
        public IReadOnlyList<string> DistinctRarityNames { get; }

        public CardDataRepository(ILogger<CardDataRepository> logger, IHttpClientFactory httpClientFactory, IConfiguration config)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _cacheTtlDays = config.GetValue<int>("CardDataSettings:CacheTtlDays", 7);

            _cards = LoadCards();
            _browseableCards = _cards.Where(c => c.CardSets?.Any() == true).ToList();
            _artworks = BuildArtworkList(_cards);
            _browseableArtworks = BuildArtworkList(_browseableCards);
            _cardIndex = _cards.ToDictionary(c => c.ID);
            _setNamesByCode = BuildSetNameIndex(_cards);
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
                .Distinct()
                .OrderBy(r => r)
                .ToList();
        }

        public IEnumerable<(Card Card, Image Image)> GetAllArtworks() => _artworks;

        public IEnumerable<(Card Card, Image Image)> GetBrowseableArtworks() => _browseableArtworks;

        public IEnumerable<Card> GetAllCards() => _cards;

        public IEnumerable<Card> GetBrowseableCards() => _browseableCards;

        public Card? GetCardByID(int cardID) =>
            _cardIndex.GetValueOrDefault(cardID);

        public IReadOnlyDictionary<string, string> GetSetNamesByCode() => _setNamesByCode;

        private static IReadOnlyDictionary<string, string> BuildSetNameIndex(IReadOnlyList<Card> cards)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var card in cards)
            {
                foreach (var set in card.CardSets ?? [])
                {
                    if (!string.IsNullOrEmpty(set.Code) && !dict.ContainsKey(set.Code))
                        dict[set.Code] = set.Name;
                }
            }
            return dict;
        }

        private static IReadOnlyList<(Card, Image)> BuildArtworkList(IReadOnlyList<Card> cards)
        {
            var artworks = new List<(Card, Image)>();
            foreach (var card in cards)
            {
                if (card.CardImages is null)
                    continue;
                foreach (var image in card.CardImages)
                    artworks.Add((card, image));
            }
            return artworks;
        }

        private IReadOnlyList<Card> LoadCards()
        {
            var cacheDir = Path.Combine(Directory.GetCurrentDirectory(), "Data");
            var cachePath = Path.Combine(cacheDir, "cardcache.json");
            var timestampPath = cachePath + ".timestamp";

            if (IsCacheFresh(cachePath, timestampPath))
            {
                _logger.LogInformation("Loading card data from cache ({Path})", cachePath);
                return DeserializeFromFile(cachePath);
            }

            _logger.LogInformation("Card cache is missing or stale — fetching from YGOProDeck API");
            var json = Task.Run(FetchFromApiAsync).GetAwaiter().GetResult();

            if (json is not null)
            {
                Directory.CreateDirectory(cacheDir);
                File.WriteAllText(cachePath, json);
                File.WriteAllText(timestampPath, DateTime.UtcNow.ToString("O"));
                _logger.LogInformation("Card data cached to {Path}", cachePath);
                return Deserialize(json);
            }

            if (File.Exists(cachePath))
            {
                _logger.LogWarning("API fetch failed — falling back to stale card cache");
                return DeserializeFromFile(cachePath);
            }

            _logger.LogError("No card data available — API fetch failed and no cache exists");
            return [];
        }

        private bool IsCacheFresh(string cachePath, string timestampPath)
        {
            if (!File.Exists(cachePath) || !File.Exists(timestampPath))
                return false;

            var raw = File.ReadAllText(timestampPath);
            if (!DateTime.TryParse(raw, null, System.Globalization.DateTimeStyles.RoundtripKind, out var cachedAt))
                return false;

            return DateTime.UtcNow - cachedAt < TimeSpan.FromDays(_cacheTtlDays);
        }

        private async Task<string?> FetchFromApiAsync()
        {
            try
            {
                var client = _httpClientFactory.CreateClient("YGOProDeck");
                return await client.GetStringAsync("api/v7/cardinfo.php");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch card data from YGOProDeck API");
                return null;
            }
        }

        private IReadOnlyList<Card> DeserializeFromFile(string path)
        {
            var json = File.ReadAllText(path);
            return Deserialize(json);
        }

        private IReadOnlyList<Card> Deserialize(string json)
        {
            try
            {
                var cardArray = JsonConvert.DeserializeObject<CardArray>(json);
                var cards = (cardArray?.Cards ?? []).ToList();
                foreach (var card in cards)
                {
                    if (card.CardSets is not null)
                        card.CardSets = card.CardSets
                            .Where(s => !s.Name.Contains("Speed Duel", StringComparison.OrdinalIgnoreCase))
                            .ToList();
                }
                return cards;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize card data");
                return [];
            }
        }
    }
}
