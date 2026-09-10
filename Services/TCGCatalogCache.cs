using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;
using CardCollector.Data;
using CardCollector.DTO;
using Newtonsoft.Json;

namespace CardCollector.Services
{
    public sealed partial class TCGCatalogCache : ITCGCatalogCache
    {
        private const int REQUEST_DELAY_MS = 200;
        private const int YUGIOH_CATEGORY_ID = 2;

        /// <summary>
        /// Print-variant qualifiers already confirmed in tcgcsv product names. Not used to filter — every
        /// non-redundant qualifier is kept regardless — only to flag genuinely new ones for review via logging.
        /// </summary>
        private static readonly IReadOnlySet<string> KnownVariantQualifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Extended Art",
            "Alternate Art",
            "Retail Exclusive"
        };

        private readonly TimeSpan _cacheTtl;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<TCGCatalogCache> _logger;
        private IReadOnlyList<TCGPriceSet> _printings;

        [ExcludeFromCodeCoverage(Justification = "Loads cached/live catalog data from disk and HTTP on construction; I/O orchestration, not testable logic.")]
        public TCGCatalogCache(ILogger<TCGCatalogCache> logger, IHttpClientFactory httpClientFactory, IConfiguration config)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _cacheTtl = TimeSpan.FromHours(config.GetValue<int>("CardDataSettings:PricingCacheTtlHours", 20));

            _printings = LoadCatalog();
        }

        /// <summary>
        /// Pure join of one tcgcsv.com group's products and prices into printings, parsing each product's name for
        /// a print-variant qualifier (public for direct unit testing). Skips products with no set code/rarity and
        /// price rows with no market price.
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

                var (cardName, printVariant) = ParseProductName(product.Name, rarityName);

                foreach (var price in pricesByProductID[product.ProductID])
                {
                    yield return new TCGPriceSet
                    {
                        CardName = cardName,
                        Code = setCode,
                        Edition = price.SubTypeName ?? string.Empty,
                        PriceRaw = price.MarketPrice!.Value.ToString(CultureInfo.InvariantCulture),
                        PrintVariant = printVariant,
                        RarityName = rarityName
                    };
                }
            }
        }

        /// <summary>
        /// Strips trailing parenthetical qualifiers off a tcgcsv product name (e.g. "Dark Magical Curtain
        /// (Extended Art)"), returning the base card name and — for any qualifier that isn't just a restatement
        /// of the product's own rarity (name or abbreviation) — the print variant (public for direct unit testing).
        /// Multiple non-redundant qualifiers are joined with ", "; none found yields a null variant.
        /// </summary>
        public static (string CardName, string? PrintVariant) ParseProductName(string productName, string? rarityName)
        {
            var name = productName.Trim();
            var variants = new List<string>();

            var match = TrailingParenRegex().Match(name);
            while (match.Success)
            {
                var qualifier = match.Groups[1].Value.Trim();
                name = name[..match.Index].TrimEnd();

                if (!IsRedundantRarityQualifier(qualifier, rarityName))
                    variants.Insert(0, qualifier);

                match = TrailingParenRegex().Match(name);
            }

            return (name, variants.Count > 0 ? string.Join(", ", variants) : null);
        }

        public IReadOnlyList<TCGPriceSet> GetAllPrintings() => _printings;

        [ExcludeFromCodeCoverage(Justification = "Re-downloads the full catalog regardless of cache freshness; I/O, not testable logic.")]
        public async Task RefreshAsync()
        {
            var cacheDir = Path.Combine(Directory.GetCurrentDirectory(), "Data");
            FileCacheHelper.TryDeleteFile(Path.Combine(cacheDir, "tcgcatalogcache.json.timestamp"));
            _printings = await Task.Run(LoadCatalog).ConfigureAwait(false);
        }

        [ExcludeFromCodeCoverage(Justification = "Single tcgcsv.com HTTP GET + envelope deserialization; I/O, not testable logic.")]
        private static async Task<IReadOnlyList<T>?> FetchResultsAsync<T>(HttpClient client, string path)
        {
            var json = await client.GetStringAsync(path).ConfigureAwait(false);
            var envelope = JsonConvert.DeserializeObject<TCGCatalogEnvelope<T>>(json);
            return envelope?.Results?.ToList();
        }

        private static bool IsRedundantRarityQualifier(string qualifier, string? rarityName)
        {
            if (string.IsNullOrWhiteSpace(rarityName))
                return false;

            if (string.Equals(qualifier, rarityName, StringComparison.OrdinalIgnoreCase))
                return true;

            var code = RarityExtensions.GetRarityCode(rarityName)?.Trim('(', ')');
            return code is not null && string.Equals(qualifier, code, StringComparison.OrdinalIgnoreCase);
        }

        [GeneratedRegex(@"\(([^()]+)\)\s*$")]
        private static partial Regex TrailingParenRegex();
        [ExcludeFromCodeCoverage(Justification = "HTTP fetch orchestration across the full tcgcsv.com group crawl; I/O, not testable logic.")]
        private async Task<IReadOnlyList<TCGPriceSet>?> FetchCatalogAsync()
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

                var entries = new List<TCGPriceSet>();
                var failedGroups = 0;

                foreach (var group in groups)
                {
                    try
                    {
                        var products = await FetchResultsAsync<TCGCatalogProduct>(client, $"tcgplayer/{YUGIOH_CATEGORY_ID}/{group.GroupID}/products").ConfigureAwait(false);
                        await Task.Delay(REQUEST_DELAY_MS).ConfigureAwait(false);
                        var prices = await FetchResultsAsync<TCGCatalogPrice>(client, $"tcgplayer/{YUGIOH_CATEGORY_ID}/{group.GroupID}/prices").ConfigureAwait(false);
                        await Task.Delay(REQUEST_DELAY_MS).ConfigureAwait(false);

                        entries.AddRange(BuildGroupEntries(products ?? [], prices ?? []));
                    }
                    catch (Exception ex)
                    {
                        failedGroups++;
                        _logger.LogWarning(ex, "Failed to fetch tcgcsv.com catalog data for group {GroupID} ({Abbreviation})", group.GroupID, group.Abbreviation);
                    }
                }

                LogUnrecognizedVariantQualifiers(entries);

                _logger.LogInformation(
                    "Fetched tcgcsv.com catalog for {SucceededCount}/{TotalCount} groups ({EntryCount} printings)",
                    groups.Count - failedGroups, groups.Count, entries.Count);
                return entries;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch bulk catalog data from tcgcsv.com");
                return null;
            }
        }

        [ExcludeFromCodeCoverage(Justification = "Cache-freshness check plus file/HTTP fallback orchestration; I/O, not testable logic.")]
        private IReadOnlyList<TCGPriceSet> LoadCatalog()
        {
            var cacheDir = Path.Combine(Directory.GetCurrentDirectory(), "Data");
            var cachePath = Path.Combine(cacheDir, "tcgcatalogcache.json");
            var timestampPath = cachePath + ".timestamp";

            if (FileCacheHelper.IsCacheFresh(cachePath, timestampPath, _cacheTtl))
            {
                _logger.LogInformation("Loading catalog data from cache ({Path})", cachePath);
                return LoadEntriesFromJson(cachePath);
            }

            _logger.LogInformation("Catalog data cache is missing or stale — fetching from tcgcsv.com");
            var entries = Task.Run(FetchCatalogAsync).GetAwaiter().GetResult();

            if (entries is not null)
            {
                Directory.CreateDirectory(cacheDir);
                File.WriteAllText(cachePath, JsonConvert.SerializeObject(entries));
                FileCacheHelper.WriteTimestamp(timestampPath);
                _logger.LogInformation("Catalog data cached to {Path} ({Count} printings)", cachePath, entries.Count);
                return entries;
            }

            if (File.Exists(cachePath))
            {
                _logger.LogWarning("tcgcsv.com catalog fetch failed — falling back to stale catalog cache");
                return LoadEntriesFromJson(cachePath);
            }

            _logger.LogError("No catalog data available — fetch failed and no cache exists");
            return [];
        }

        [ExcludeFromCodeCoverage(Justification = "Reads catalog JSON from disk; I/O, not testable logic.")]
        private IReadOnlyList<TCGPriceSet> LoadEntriesFromJson(string path)
        {
            try
            {
                var json = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<List<TCGPriceSet>>(json) ?? [];
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize catalog data from {Path}", path);
                return [];
            }
        }
        private void LogUnrecognizedVariantQualifiers(IReadOnlyList<TCGPriceSet> entries)
        {
            var unrecognized = entries
                .Select(e => e.PrintVariant)
                .Where(v => !string.IsNullOrEmpty(v) && !KnownVariantQualifiers.Contains(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (unrecognized.Count > 0)
                _logger.LogWarning(
                    "Found {Count} unrecognized print-variant qualifier(s) in tcgcsv.com data (treated as distinct variants, not dropped): {Qualifiers}",
                    unrecognized.Count, string.Join(", ", unrecognized));
        }
    }
}
