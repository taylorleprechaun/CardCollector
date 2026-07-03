using CardCollector.Data;
using CardCollector.DTO;
using Newtonsoft.Json;

namespace CardCollector.Services
{
    public sealed class PricingDataCache : IPricingDataCache
    {
        private const string CardInfoApiPath = "api/v7/cardinfo.php";
        private const int PageSize = 5000;

        private readonly TimeSpan _cacheTtl;
        private IReadOnlyDictionary<int, IReadOnlyList<TCGPriceSet>> _cardSetsByCardID;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<PricingDataCache> _logger;

        public PricingDataCache(ILogger<PricingDataCache> logger, IHttpClientFactory httpClientFactory, IConfiguration config)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _cacheTtl = TimeSpan.FromHours(config.GetValue<int>("CardDataSettings:PricingCacheTtlHours", 20));

            _cardSetsByCardID = LoadCardSets();
        }

        public IReadOnlyList<TCGPriceSet> GetCardSets(int cardID) =>
            _cardSetsByCardID.TryGetValue(cardID, out var sets) ? sets : [];

        public async Task RefreshAsync()
        {
            var cacheDir = Path.Combine(Directory.GetCurrentDirectory(), "Data");
            FileCacheHelper.TryDeleteFile(Path.Combine(cacheDir, "pricingcache.json.timestamp"));
            _cardSetsByCardID = await Task.Run(LoadCardSets).ConfigureAwait(false);
        }

        private async Task<List<TCGPriceCard>?> FetchAllCardsAsync()
        {
            try
            {
                var client = _httpClientFactory.CreateClient("YGOProDeck");
                var allCards = new List<TCGPriceCard>();
                var offset = 0;

                while (true)
                {
                    var json = await client.GetStringAsync($"{CardInfoApiPath}?tcgplayer_data=true&num={PageSize}&offset={offset}").ConfigureAwait(false);
                    var page = JsonConvert.DeserializeObject<TCGPriceCardArray>(json);
                    if (page?.Cards is null)
                        break;

                    allCards.AddRange(page.Cards);

                    if (page.Meta is null || page.Meta.RowsRemaining <= 0)
                        break;

                    offset += PageSize;
                }

                _logger.LogInformation("Fetched pricing data for {Count} cards from YGOProDeck", allCards.Count);
                return allCards;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch bulk pricing data from YGOProDeck");
                return null;
            }
        }

        private static IReadOnlyDictionary<int, IReadOnlyList<TCGPriceSet>> IndexCards(IEnumerable<TCGPriceCard> cards) =>
            cards.ToDictionary(c => c.ID, c => (IReadOnlyList<TCGPriceSet>)c.CardSets.ToList());

        private IReadOnlyDictionary<int, IReadOnlyList<TCGPriceSet>> LoadCardSets()
        {
            var cacheDir = Path.Combine(Directory.GetCurrentDirectory(), "Data");
            var cachePath = Path.Combine(cacheDir, "pricingcache.json");
            var timestampPath = cachePath + ".timestamp";

            if (FileCacheHelper.IsCacheFresh(cachePath, timestampPath, _cacheTtl))
            {
                _logger.LogInformation("Loading pricing data from cache ({Path})", cachePath);
                return IndexCards(LoadCardsFromJson(cachePath));
            }

            _logger.LogInformation("Pricing data cache is missing or stale — fetching from YGOProDeck");
            var cards = Task.Run(FetchAllCardsAsync).GetAwaiter().GetResult();

            if (cards is not null)
            {
                Directory.CreateDirectory(cacheDir);
                File.WriteAllText(cachePath, JsonConvert.SerializeObject(cards));
                FileCacheHelper.WriteTimestamp(timestampPath);
                _logger.LogInformation("Pricing data cached to {Path} ({Count} cards)", cachePath, cards.Count);
                return IndexCards(cards);
            }

            if (File.Exists(cachePath))
            {
                _logger.LogWarning("YGOProDeck pricing fetch failed — falling back to stale pricing cache");
                return IndexCards(LoadCardsFromJson(cachePath));
            }

            _logger.LogError("No pricing data available — fetch failed and no cache exists");
            return new Dictionary<int, IReadOnlyList<TCGPriceSet>>();
        }

        private List<TCGPriceCard> LoadCardsFromJson(string path)
        {
            try
            {
                var json = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<List<TCGPriceCard>>(json) ?? [];
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize pricing data from {Path}", path);
                return [];
            }
        }
    }
}
