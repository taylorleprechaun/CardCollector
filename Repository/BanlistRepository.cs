using CardCollector.Data;
using CardCollector.Models;
using CardCollector.Rules;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

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

        /// <summary>How long a failed first-use fetch is trusted before a request tries the network again.</summary>
        private static readonly TimeSpan FailedFetchRetryDelay = TimeSpan.FromMinutes(5);

        private readonly string _cachePath;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly SemaphoreSlim _loadLock = new(1, 1);
        private readonly ILogger<BanlistRepository> _logger;
        private readonly BanlistSettings _settings;
        private readonly TimeProvider _timeProvider;
        private readonly string _timestampPath;
        private volatile BanlistCollection? _collection;

        /// <summary>When the last first-use fetch failed; only read or written while holding <see cref="_loadLock"/>.</summary>
        private DateTimeOffset? _lastFailedFetch;

        /// <param name="cacheDirectory">Overrides the cache location for tests; defaults to the app's <c>Data</c> directory.</param>
        /// <param name="timeProvider">Overrides the clock for tests; defaults to the system clock.</param>
        public BanlistRepository(
            ILogger<BanlistRepository> logger,
            IHttpClientFactory httpClientFactory,
            IOptions<BanlistSettings> options,
            string? cacheDirectory = null,
            TimeProvider? timeProvider = null)
        {
            if (options is null) throw new ArgumentNullException(nameof(options));

            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _settings = options.Value;
            _timeProvider = timeProvider ?? TimeProvider.System;

            var cacheDir = cacheDirectory ?? Path.Combine(Directory.GetCurrentDirectory(), "Data");
            _cachePath = Path.Combine(cacheDir, "banlistcache.json");
            _timestampPath = _cachePath + ".timestamp";

            _collection = LoadFromDisk();
        }

        public async Task<IReadOnlyList<DateOnly>> GetAvailableListsAsync(CancellationToken cancellationToken = default)
        {
            await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
            return _collection?.Dates ?? [];
        }

        public async Task<Banlist?> GetCurrentAsync(CancellationToken cancellationToken = default)
        {
            await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
            return _collection?.Current;
        }

        public async Task<Banlist?> GetListAsync(DateOnly effectiveDate, CancellationToken cancellationToken = default)
        {
            await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
            return _collection?.GetByDate(effectiveDate);
        }

        public async Task<Banlist?> GetListForDateAsync(DateOnly date, CancellationToken cancellationToken = default)
        {
            await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
            return _collection?.GetForDate(date);
        }

        public async Task LoadIfStaleAsync(CancellationToken cancellationToken = default)
        {
            if (FileCacheHelper.IsCacheFresh(_cachePath, _timestampPath, TimeSpan.FromDays(_settings.CacheTtlDays)))
            {
                _logger.LogInformation("Banlist cache already fresh — skipping warm-up fetch");
                return;
            }

            await _loadLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // A concurrent caller may have refreshed while this one waited for the lock.
                if (!FileCacheHelper.IsCacheFresh(_cachePath, _timestampPath, TimeSpan.FromDays(_settings.CacheTtlDays)))
                {
                    _logger.LogInformation("Banlist cache is missing or stale — fetching from yaml-yugi-limit-regulation");
                    await RefreshFromSourceAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                _loadLock.Release();
            }
        }

        /// <summary>The cache is app-written, but a truncated or hand-edited file can still leave a list or its limits null.</summary>
        private static bool IsComplete([NotNullWhen(true)] Banlist? list) =>
            list?.LimitsByKonamiID is not null;

        /// <summary>
        /// Fetches on first use when nothing was on disk at startup. An existing (even stale) cache is served as-is;
        /// only <see cref="LoadIfStaleAsync"/> refreshes a stale one. If the fetch fails, requests don't try again
        /// until <see cref="FailedFetchRetryDelay"/> has passed, so an unreachable source can't stall every page that
        /// needs a banlist.
        /// </summary>
        private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
        {
            if (_collection is not null)
                return;

            await _loadLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_collection is not null)
                    return;

                var now = _timeProvider.GetUtcNow();
                if (_lastFailedFetch is { } failedAt && now - failedAt < FailedFetchRetryDelay)
                    return;

                _logger.LogInformation("No banlist cache on disk — fetching from yaml-yugi-limit-regulation");
                await RefreshFromSourceAsync(cancellationToken).ConfigureAwait(false);
                _lastFailedFetch = _collection is null ? now : null;
            }
            finally
            {
                _loadLock.Release();
            }
        }

        private async Task<(IReadOnlyList<Banlist> Lists, int FailedCount)> FetchDatedListsAsync(HttpClient client, IReadOnlyList<string> datedListNames, CancellationToken cancellationToken)
        {
            var lists = new ConcurrentBag<Banlist>();
            var failedCount = 0;

            using var throttle = new SemaphoreSlim(MAX_CONCURRENT_LIST_FETCHES);
            await Task.WhenAll(datedListNames.Select(async name =>
            {
                await throttle.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    var list = await FetchListAsync(client, $"{_settings.BaseUrl}tcg/{name}", cancellationToken).ConfigureAwait(false);
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

        private async Task<IReadOnlyList<string>?> FetchIndexAsync(HttpClient client, CancellationToken cancellationToken)
        {
            try
            {
                var json = await client.GetStringAsync(_settings.IndexUrl, cancellationToken).ConfigureAwait(false);
                var entries = JsonConvert.DeserializeObject<List<GitHubContentEntry>>(json);
                if (entries is null)
                    return null;

                _logger.LogInformation("Fetched banlist index from {Url} ({Count} entries)", _settings.IndexUrl, entries.Count);
                return entries.Where(e => e.Name is not null).Select(e => e.Name!).ToList();
            }
            // A cancelled caller propagates; any other failure, an HttpClient timeout included, is logged and treated as no data.
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Failed to fetch banlist index from {Url}", _settings.IndexUrl);
                return null;
            }
        }

        private async Task<Banlist?> FetchListAsync(HttpClient client, string url, CancellationToken cancellationToken)
        {
            try
            {
                var json = await client.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
                var list = BanlistParser.ParseList(json, _logger);
                if (list is null)
                    _logger.LogWarning("Failed to parse banlist at {Url}", url);
                return list;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
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
                if (root?.Lists is not { } lists || !lists.All(IsComplete) || (root.Current is not null && !IsComplete(root.Current)))
                {
                    _logger.LogWarning("Banlist cache at {Path} is incomplete — treating it as missing", _cachePath);
                    return null;
                }

                return BanlistCollection.Build(lists.OfType<Banlist>().ToList(), root.Current, _settings.EffectiveDateOverrides, _logger);
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

        private async Task RefreshFromSourceAsync(CancellationToken cancellationToken)
        {
            var client = _httpClientFactory.CreateClient("YamlYugiLimitRegulation");

            var fileNames = await FetchIndexAsync(client, cancellationToken).ConfigureAwait(false);
            var current = fileNames is null ? null : await FetchListAsync(client, $"{_settings.BaseUrl}tcg/current.vector.json", cancellationToken).ConfigureAwait(false);

            if (fileNames is null || current is null)
            {
                LogFetchFailure("the banlist index or current list");
                return;
            }

            var datedListNames = BanlistParser.FilterDatedListNames(fileNames);
            var (lists, failedCount) = await FetchDatedListsAsync(client, datedListNames, cancellationToken).ConfigureAwait(false);

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

            var root = new BanlistCacheRoot { Current = current, Lists = [.. lists] };
            File.WriteAllText(_cachePath, JsonConvert.SerializeObject(root));
            FileCacheHelper.WriteTimestamp(_timestampPath);
        }

        private sealed class BanlistCacheRoot
        {
            public Banlist? Current { get; set; }

            public List<Banlist?>? Lists { get; set; }
        }

        private sealed class GitHubContentEntry
        {
            [JsonProperty("name")]
            public string? Name { get; set; }
        }
    }
}
