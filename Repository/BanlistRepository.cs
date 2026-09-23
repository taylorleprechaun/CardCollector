using CardCollector.Data;
using CardCollector.Models;
using CardCollector.Rules;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Collections.Concurrent;

namespace CardCollector.Repository
{
    /// <summary>
    /// Fetches and caches banlist data from yaml-yugi-limit-regulation. Singleton: holds the parsed lists in
    /// memory and swaps them atomically on refresh. Date overrides are applied when the cache is loaded, not stored in it,
    /// so a config-only change takes effect on restart without a refetch.
    /// </summary>
    public sealed class BanlistRepository : IBanlistRepository
    {
        private const int MAX_CONCURRENT_LIST_FETCHES = 8;

        private readonly string _cachePath;
        private BanlistCollection? _collection;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly SemaphoreSlim _loadLock = new(1, 1);
        private readonly ILogger<BanlistRepository> _logger;
        private readonly BanlistSettings _settings;
        private readonly string _timestampPath;

        /// <param name="cacheDirectory">Overrides the cache location for tests; defaults to the app's <c>Data</c> directory.</param>
        public BanlistRepository(
            ILogger<BanlistRepository> logger,
            IHttpClientFactory httpClientFactory,
            IOptions<BanlistSettings> options,
            string? cacheDirectory = null)
        {
            if (options is null) throw new ArgumentNullException(nameof(options));

            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _settings = options.Value;

            var cacheDir = cacheDirectory ?? Path.Combine(Directory.GetCurrentDirectory(), "Data");
            _cachePath = Path.Combine(cacheDir, "banlistcache.json");
            _timestampPath = _cachePath + ".timestamp";

            _collection = LoadFromDisk();
        }

        public async Task<IReadOnlyList<DateOnly>> GetAvailableListsAsync()
        {
            await EnsureLoadedAsync().ConfigureAwait(false);
            return _collection?.Dates ?? [];
        }

        public async Task<Banlist?> GetCurrentAsync()
        {
            await EnsureLoadedAsync().ConfigureAwait(false);
            return _collection?.Current;
        }

        public async Task<Banlist?> GetListAsync(DateOnly effectiveDate)
        {
            await EnsureLoadedAsync().ConfigureAwait(false);
            return _collection?.GetByDate(effectiveDate);
        }

        public async Task<Banlist?> GetListForDateAsync(DateOnly date)
        {
            await EnsureLoadedAsync().ConfigureAwait(false);
            return _collection?.GetForDate(date);
        }

        public async Task LoadIfStaleAsync()
        {
            if (FileCacheHelper.IsCacheFresh(_cachePath, _timestampPath, TimeSpan.FromDays(_settings.CacheTtlDays)))
            {
                _logger.LogInformation("Banlist cache already fresh — skipping warm-up fetch");
                return;
            }

            await _loadLock.WaitAsync().ConfigureAwait(false);
            try
            {
                // A concurrent caller may have refreshed while this one waited for the lock.
                if (!FileCacheHelper.IsCacheFresh(_cachePath, _timestampPath, TimeSpan.FromDays(_settings.CacheTtlDays)))
                {
                    _logger.LogInformation("Banlist cache is missing or stale — fetching from yaml-yugi-limit-regulation");
                    await RefreshFromSourceAsync().ConfigureAwait(false);
                }
            }
            finally
            {
                _loadLock.Release();
            }
        }

        /// <summary>Fetches on first use only, when nothing was on disk at startup. An existing (even stale) cache is
        /// served as-is; only <see cref="LoadIfStaleAsync"/> refreshes a stale one, so requests never block on the network.</summary>
        private async Task EnsureLoadedAsync()
        {
            if (_collection is not null)
                return;

            await _loadLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_collection is null)
                {
                    _logger.LogInformation("No banlist cache on disk — fetching from yaml-yugi-limit-regulation");
                    await RefreshFromSourceAsync().ConfigureAwait(false);
                }
            }
            finally
            {
                _loadLock.Release();
            }
        }

        private async Task<(IReadOnlyList<Banlist> Lists, int FailedCount)> FetchDatedListsAsync(HttpClient client, IReadOnlyList<string> datedListNames)
        {
            var lists = new ConcurrentBag<Banlist>();
            var failedCount = 0;

            using var throttle = new SemaphoreSlim(MAX_CONCURRENT_LIST_FETCHES);
            await Task.WhenAll(datedListNames.Select(async name =>
            {
                await throttle.WaitAsync().ConfigureAwait(false);
                try
                {
                    var list = await FetchListAsync(client, $"{_settings.BaseUrl}tcg/{name}").ConfigureAwait(false);
                    if (list is null)
                        Interlocked.Increment(ref failedCount);
                    else
                        lists.Add(list);
                }
                finally
                {
                    throttle.Release();
                }
            })).ConfigureAwait(false);

            return (lists.ToList(), failedCount);
        }

        private async Task<IReadOnlyList<string>?> FetchIndexAsync(HttpClient client)
        {
            try
            {
                var json = await client.GetStringAsync(_settings.IndexUrl).ConfigureAwait(false);
                var entries = JsonConvert.DeserializeObject<List<GitHubContentEntry>>(json);
                if (entries is null)
                    return null;

                _logger.LogInformation("Fetched banlist index from {Url} ({Count} entries)", _settings.IndexUrl, entries.Count);
                return entries.Where(e => e.Name is not null).Select(e => e.Name!).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch banlist index from {Url}", _settings.IndexUrl);
                return null;
            }
        }

        private async Task<Banlist?> FetchListAsync(HttpClient client, string url)
        {
            try
            {
                var json = await client.GetStringAsync(url).ConfigureAwait(false);
                var list = BanlistParser.ParseList(json, _logger);
                if (list is null)
                    _logger.LogWarning("Failed to parse banlist at {Url}", url);
                return list;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch banlist from {Url}", url);
                return null;
            }
        }

        private BanlistCollection? LoadFromDisk()
        {
            if (!File.Exists(_cachePath))
                return null;

            try
            {
                var json = File.ReadAllText(_cachePath);
                var root = JsonConvert.DeserializeObject<BanlistCacheRoot>(json);
                return root is null
                    ? null
                    : BanlistCollection.Build(root.Lists, root.Current, _settings.EffectiveDateOverrides, _logger);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse banlist cache from {Path}", _cachePath);
                return null;
            }
        }

        private void LogFetchFailure(string what)
        {
            if (_collection is null)
                _logger.LogError(
                    "Failed to fetch {What} from yaml-yugi-limit-regulation, and no banlist cache exists — banlist unavailable",
                    what);
            else
                _logger.LogWarning(
                    "Failed to fetch {What} from yaml-yugi-limit-regulation — keeping existing banlist cache",
                    what);
        }

        private async Task RefreshFromSourceAsync()
        {
            var client = _httpClientFactory.CreateClient("YamlYugiLimitRegulation");

            var fileNames = await FetchIndexAsync(client).ConfigureAwait(false);
            var current = fileNames is null ? null : await FetchListAsync(client, $"{_settings.BaseUrl}tcg/current.vector.json").ConfigureAwait(false);

            if (fileNames is null || current is null)
            {
                LogFetchFailure("the banlist index or current list");
                return;
            }

            var datedListNames = BanlistParser.FilterDatedListNames(fileNames);
            var (lists, failedCount) = await FetchDatedListsAsync(client, datedListNames).ConfigureAwait(false);

            if (failedCount > 0)
            {
                LogFetchFailure($"{failedCount}/{datedListNames.Count} dated banlist(s)");
                return;
            }

            WriteCache(lists, current);
            _collection = BanlistCollection.Build(lists, current, _settings.EffectiveDateOverrides, _logger);
            _logger.LogInformation(
                "Banlist cache refreshed ({ListCount} dated lists, current effective {CurrentDate})",
                lists.Count, current.EffectiveDate);
        }

        private void WriteCache(IReadOnlyList<Banlist> lists, Banlist current)
        {
            var cacheDir = Path.GetDirectoryName(_cachePath)!;
            Directory.CreateDirectory(cacheDir);

            var root = new BanlistCacheRoot { Current = current, Lists = lists.ToList() };
            File.WriteAllText(_cachePath, JsonConvert.SerializeObject(root));
            FileCacheHelper.WriteTimestamp(_timestampPath);
        }

        private sealed class BanlistCacheRoot
        {
            public Banlist? Current { get; set; }

            public List<Banlist> Lists { get; set; } = [];
        }

        private sealed class GitHubContentEntry
        {
            [JsonProperty("name")]
            public string? Name { get; set; }
        }
    }
}
