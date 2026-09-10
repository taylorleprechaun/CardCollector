using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CardCollector.Data;
using CardCollector.DTO;
using CardCollector.Repository;
using Newtonsoft.Json;

namespace CardCollector.Services
{
    public sealed class PricingDataCache : IPricingDataCache
    {
        private const int REQUEST_DELAY_MS = 200;
        private const int YUGIOH_CATEGORY_ID = 2;

        private readonly TimeSpan _cacheTtl;
        private readonly ICardDataRepository _cardDataRepository;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<PricingDataCache> _logger;
        private IReadOnlyDictionary<(string SetCode, string RarityName), IReadOnlyList<TCGPriceSet>> _pricingIndex;

        [ExcludeFromCodeCoverage(Justification = "Loads cached/live pricing data from disk and HTTP on construction; I/O orchestration, not testable logic.")]
        public PricingDataCache(ILogger<PricingDataCache> logger, ICardDataRepository cardDataRepository, IHttpClientFactory httpClientFactory, IConfiguration config)
        {
            _logger = logger;
            _cardDataRepository = cardDataRepository;
            _httpClientFactory = httpClientFactory;
            _cacheTtl = TimeSpan.FromHours(config.GetValue<int>("CardDataSettings:PricingCacheTtlHours", 20));

            _pricingIndex = LoadPricingIndex();
        }

        /// <summary>
        /// Groups entries by (SetCode, RarityName) into <paramref name="target"/>, overwriting any existing
        /// entries for a key (public for direct unit testing; last-applied group wins on cross-set-code collisions).
        /// </summary>
        public static void ApplyGroupEntries(
            IDictionary<(string SetCode, string RarityName), List<TCGPriceSet>> target,
            IEnumerable<TCGPriceSet> groupEntries)
        {
            foreach (var group in groupEntries.GroupBy(e =>
                (e.Code.ToUpperInvariant(), (RarityExtensions.NormalizeRarityName(e.RarityName) ?? e.RarityName).ToUpperInvariant())))
            {
                target[group.Key] = group.ToList();
            }
        }

        /// <summary>
        /// Pure join of one tcgcsv.com group's products and prices into printings (public for direct unit testing).
        /// Skips products with no set code/rarity and price rows with no market price.
        /// </summary>
        public static IEnumerable<TCGPriceSet> BuildGroupEntries(IEnumerable<TCGCatalogProduct> products, IEnumerable<TCGCatalogPrice> prices)
        {
            var pricesByProductID = prices
                .Where(p => p.MarketPrice.HasValue)
                .ToLookup(p => p.ProductID);

            foreach (var product in products)
            {
                var setCode = product.ExtendedData.FirstOrDefault(f => f.Name == "Number")?.Value;
                var rarityName = product.ExtendedData.FirstOrDefault(f => f.Name == "Rarity")?.Value;
                if (string.IsNullOrWhiteSpace(setCode) || string.IsNullOrWhiteSpace(rarityName))
                    continue;

                foreach (var price in pricesByProductID[product.ProductID])
                {
                    yield return new TCGPriceSet
                    {
                        Code = setCode,
                        Edition = price.SubTypeName ?? string.Empty,
                        PriceRaw = price.MarketPrice!.Value.ToString(CultureInfo.InvariantCulture),
                        RarityName = rarityName
                    };
                }
            }
        }

        /// <summary>
        /// Looks up a card's known printings (via <see cref="ICardDataRepository"/>) in the pricing index, since
        /// tcgcsv.com has no concept of the card's numeric ID (public for direct unit testing).
        /// </summary>
        public static IReadOnlyList<TCGPriceSet> LookupCardSets(
            Card? card,
            IReadOnlyDictionary<(string SetCode, string RarityName), IReadOnlyList<TCGPriceSet>> pricingIndex)
        {
            if (card?.CardSets is null)
                return [];

            var matches = new List<TCGPriceSet>();
            foreach (var set in card.CardSets)
            {
                if (string.IsNullOrWhiteSpace(set.Code))
                    continue;

                var key = (set.Code.ToUpperInvariant(), (RarityExtensions.NormalizeRarityName(set.RarityName) ?? set.RarityName ?? string.Empty).ToUpperInvariant());
                if (pricingIndex.TryGetValue(key, out var priceSets))
                    matches.AddRange(priceSets);
            }

            return matches;
        }

        public IReadOnlyList<TCGPriceSet> GetCardSets(int cardID) =>
            LookupCardSets(_cardDataRepository.GetCardByID(cardID), _pricingIndex);

        [ExcludeFromCodeCoverage(Justification = "Re-downloads the full pricing dataset regardless of cache freshness; I/O, not testable logic.")]
        public async Task RefreshAsync()
        {
            var cacheDir = Path.Combine(Directory.GetCurrentDirectory(), "Data");
            FileCacheHelper.TryDeleteFile(Path.Combine(cacheDir, "tcgpricingcache.json.timestamp"));
            _pricingIndex = await Task.Run(LoadPricingIndex).ConfigureAwait(false);
        }

        private static IReadOnlyDictionary<(string SetCode, string RarityName), IReadOnlyList<TCGPriceSet>> BuildReadOnlyIndex(IEnumerable<TCGPriceSet> entries)
        {
            var index = new Dictionary<(string SetCode, string RarityName), List<TCGPriceSet>>();
            ApplyGroupEntries(index, entries);
            return Freeze(index);
        }

        [ExcludeFromCodeCoverage(Justification = "Single tcgcsv.com HTTP GET + envelope deserialization; I/O, not testable logic.")]
        private static async Task<IReadOnlyList<T>?> FetchResultsAsync<T>(HttpClient client, string path)
        {
            var json = await client.GetStringAsync(path).ConfigureAwait(false);
            var envelope = JsonConvert.DeserializeObject<TCGCatalogEnvelope<T>>(json);
            return envelope?.Results?.ToList();
        }

        private static IReadOnlyDictionary<(string SetCode, string RarityName), IReadOnlyList<TCGPriceSet>> Freeze(
            IDictionary<(string SetCode, string RarityName), List<TCGPriceSet>> index) =>
            index.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<TCGPriceSet>)kv.Value);

        [ExcludeFromCodeCoverage(Justification = "HTTP fetch orchestration across the full tcgcsv.com group crawl; I/O, not testable logic.")]
        private async Task<IReadOnlyDictionary<(string SetCode, string RarityName), IReadOnlyList<TCGPriceSet>>?> FetchPricingIndexAsync()
        {
            try
            {
                var client = _httpClientFactory.CreateClient("TcgCsv");
                var groups = await FetchResultsAsync<TCGCatalogGroup>(client, $"tcgplayer/{YUGIOH_CATEGORY_ID}/groups").ConfigureAwait(false);

                if (groups is null || groups.Count == 0)
                {
                    _logger.LogError("Failed to fetch tcgcsv.com group list — no groups returned");
                    return null;
                }

                var index = new Dictionary<(string SetCode, string RarityName), List<TCGPriceSet>>();
                var failedGroups = 0;

                foreach (var group in groups)
                {
                    try
                    {
                        var products = await FetchResultsAsync<TCGCatalogProduct>(client, $"tcgplayer/{YUGIOH_CATEGORY_ID}/{group.GroupID}/products").ConfigureAwait(false);
                        await Task.Delay(REQUEST_DELAY_MS).ConfigureAwait(false);
                        var prices = await FetchResultsAsync<TCGCatalogPrice>(client, $"tcgplayer/{YUGIOH_CATEGORY_ID}/{group.GroupID}/prices").ConfigureAwait(false);
                        await Task.Delay(REQUEST_DELAY_MS).ConfigureAwait(false);

                        ApplyGroupEntries(index, BuildGroupEntries(products ?? [], prices ?? []));
                    }
                    catch (Exception ex)
                    {
                        failedGroups++;
                        _logger.LogWarning(ex, "Failed to fetch tcgcsv.com pricing for group {GroupID} ({Abbreviation})", group.GroupID, group.Abbreviation);
                    }
                }

                _logger.LogInformation(
                    "Fetched tcgcsv.com pricing for {SucceededCount}/{TotalCount} groups ({EntryCount} printings indexed)",
                    groups.Count - failedGroups, groups.Count, index.Count);
                return Freeze(index);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch bulk pricing data from tcgcsv.com");
                return null;
            }
        }

        [ExcludeFromCodeCoverage(Justification = "Reads pricing JSON from disk; I/O, not testable logic.")]
        private IReadOnlyList<TCGPriceSet> LoadEntriesFromJson(string path)
        {
            try
            {
                var json = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<List<TCGPriceSet>>(json) ?? [];
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize pricing data from {Path}", path);
                return [];
            }
        }

        [ExcludeFromCodeCoverage(Justification = "Cache-freshness check plus file/HTTP fallback orchestration; I/O, not testable logic.")]
        private IReadOnlyDictionary<(string SetCode, string RarityName), IReadOnlyList<TCGPriceSet>> LoadPricingIndex()
        {
            var cacheDir = Path.Combine(Directory.GetCurrentDirectory(), "Data");
            var cachePath = Path.Combine(cacheDir, "tcgpricingcache.json");
            var timestampPath = cachePath + ".timestamp";

            if (FileCacheHelper.IsCacheFresh(cachePath, timestampPath, _cacheTtl))
            {
                _logger.LogInformation("Loading pricing data from cache ({Path})", cachePath);
                return BuildReadOnlyIndex(LoadEntriesFromJson(cachePath));
            }

            _logger.LogInformation("Pricing data cache is missing or stale — fetching from tcgcsv.com");
            var index = Task.Run(FetchPricingIndexAsync).GetAwaiter().GetResult();

            if (index is not null)
            {
                var entries = index.Values.SelectMany(v => v).ToList();
                Directory.CreateDirectory(cacheDir);
                File.WriteAllText(cachePath, JsonConvert.SerializeObject(entries));
                FileCacheHelper.WriteTimestamp(timestampPath);
                _logger.LogInformation("Pricing data cached to {Path} ({Count} printings)", cachePath, entries.Count);
                return index;
            }

            if (File.Exists(cachePath))
            {
                _logger.LogWarning("tcgcsv.com pricing fetch failed — falling back to stale pricing cache");
                return BuildReadOnlyIndex(LoadEntriesFromJson(cachePath));
            }

            _logger.LogError("No pricing data available — fetch failed and no cache exists");
            return new Dictionary<(string SetCode, string RarityName), IReadOnlyList<TCGPriceSet>>();
        }
    }
}
