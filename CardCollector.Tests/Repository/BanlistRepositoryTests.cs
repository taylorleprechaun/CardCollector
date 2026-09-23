using System.Net;
using CardCollector.Models;
using CardCollector.Repository;
using CardCollector.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json;

namespace CardCollector.Tests.Repository
{
    [TestClass]
    public sealed class BanlistRepositoryTests
    {
        private string _cacheDir = null!;

        [TestMethod]
        public async Task GetAvailableListsAsync_NoDataAvailable_ReturnsEmpty()
        {
            var repo = CreateRepository(BuildHandler(indexFails: true));

            var dates = await repo.GetAvailableListsAsync();

            Assert.AreEqual(0, dates.Count);
        }

        [TestMethod]
        public async Task GetCurrentAsync_CacheAlreadyOnDisk_DoesNotFetchFromNetwork()
        {
            SeedCache(new DateOnly(2024, 1, 1), ageDays: 0, (100, BanlistLimit.Forbidden));
            var repo = CreateRepository(BuildHandler(indexFails: true));

            var current = await repo.GetCurrentAsync();

            Assert.IsNotNull(current);
            Assert.AreEqual(BanlistLimit.Forbidden, current!.GetLimit(100));
        }

        [TestMethod]
        public async Task GetCurrentAsync_ConcurrentFirstUse_FetchesIndexOnlyOnce()
        {
            var indexCallCount = 0;
            var handler = new FakeHttpMessageHandler(request =>
            {
                var url = request.RequestUri!.ToString();
                if (url.EndsWith("/index", StringComparison.Ordinal))
                {
                    Interlocked.Increment(ref indexCallCount);
                    return JsonResponse(JsonConvert.SerializeObject(new[] { new { name = "2024-01-01.vector.json" } }));
                }

                return JsonResponse(BuildListJson("2024-01-01", (100, 0)));
            });
            var repo = CreateRepository(handler);

            await Task.WhenAll(repo.GetCurrentAsync(), repo.GetAvailableListsAsync(), repo.GetCurrentAsync());

            Assert.AreEqual(1, indexCallCount);
        }

        [TestMethod]
        public async Task GetCurrentAsync_IndexDeserializesToNull_ReturnsNullWithoutThrowing()
        {
            var handler = new FakeHttpMessageHandler(request =>
            {
                var url = request.RequestUri!.ToString();
                return url.EndsWith("/index", StringComparison.Ordinal)
                    ? JsonResponse("null")
                    : new HttpResponseMessage(HttpStatusCode.InternalServerError);
            });
            var repo = CreateRepository(handler);

            var current = await repo.GetCurrentAsync();

            Assert.IsNull(current);
        }

        [TestMethod]
        public async Task GetCurrentAsync_MalformedCacheOnDisk_FetchesFromNetworkInstead()
        {
            Directory.CreateDirectory(_cacheDir);
            File.WriteAllText(Path.Combine(_cacheDir, "banlistcache.json"), "{ not valid json");
            File.WriteAllText(Path.Combine(_cacheDir, "banlistcache.json.timestamp"), DateTime.UtcNow.ToString("O"));
            var handler = BuildHandler(
                indexNames: ["2024-01-01.vector.json"],
                currentJson: BuildListJson("2024-01-01", (100, 0)),
                listJsonByFileName: new Dictionary<string, string> { ["2024-01-01.vector.json"] = BuildListJson("2024-01-01", (100, 0)) });
            var repo = CreateRepository(handler);

            var current = await repo.GetCurrentAsync();

            Assert.IsNotNull(current);
            Assert.AreEqual(new DateOnly(2024, 1, 1), current!.EffectiveDate);
        }

        [TestMethod]
        public async Task GetCurrentAsync_NoCacheAndFetchFails_ReturnsNullWithoutThrowing()
        {
            var repo = CreateRepository(BuildHandler(indexFails: true));

            var current = await repo.GetCurrentAsync();

            Assert.IsNull(current);
        }

        [TestMethod]
        public async Task GetCurrentAsync_NoCacheOnDisk_FetchesAndCachesToDisk()
        {
            var handler = BuildHandler(
                indexNames: ["2024-01-01.vector.json"],
                currentJson: BuildListJson("2024-01-01", (100, 0)),
                listJsonByFileName: new Dictionary<string, string> { ["2024-01-01.vector.json"] = BuildListJson("2024-01-01", (100, 0)) });
            var repo = CreateRepository(handler);

            var current = await repo.GetCurrentAsync();

            Assert.IsNotNull(current);
            Assert.AreEqual(BanlistLimit.Forbidden, current!.GetLimit(100));
            Assert.IsTrue(File.Exists(Path.Combine(_cacheDir, "banlistcache.json")));
        }

        [TestMethod]
        public async Task GetListAsync_ExactDateMatch_ReturnsThatList()
        {
            var handler = BuildHandler(
                indexNames: ["2024-01-01.vector.json"],
                currentJson: BuildListJson("2024-01-01", (100, 0)),
                listJsonByFileName: new Dictionary<string, string> { ["2024-01-01.vector.json"] = BuildListJson("2024-01-01", (100, 0)) });
            var repo = CreateRepository(handler);

            var list = await repo.GetListAsync(new DateOnly(2024, 1, 1));

            Assert.IsNotNull(list);
            Assert.AreEqual(BanlistLimit.Forbidden, list!.GetLimit(100));
        }

        [TestMethod]
        public async Task GetListForDateAsync_DateBetweenLists_ReturnsEarlierList()
        {
            var handler = BuildHandler(
                indexNames: ["2024-01-01.vector.json", "2024-06-01.vector.json"],
                currentJson: BuildListJson("2024-06-01", (200, 1)),
                listJsonByFileName: new Dictionary<string, string>
                {
                    ["2024-01-01.vector.json"] = BuildListJson("2024-01-01", (100, 0)),
                    ["2024-06-01.vector.json"] = BuildListJson("2024-06-01", (200, 1))
                });
            var repo = CreateRepository(handler);

            var list = await repo.GetListForDateAsync(new DateOnly(2024, 3, 1));

            Assert.IsNotNull(list);
            Assert.AreEqual(new DateOnly(2024, 1, 1), list!.EffectiveDate);
        }

        [TestMethod]
        public async Task LoadIfStaleAsync_CacheExpired_Refetches()
        {
            SeedCache(new DateOnly(2020, 1, 1), ageDays: 30, (999, BanlistLimit.Forbidden));
            var handler = BuildHandler(
                indexNames: ["2024-01-01.vector.json"],
                currentJson: BuildListJson("2024-01-01", (200, 1)),
                listJsonByFileName: new Dictionary<string, string> { ["2024-01-01.vector.json"] = BuildListJson("2024-01-01", (200, 1)) });
            var repo = CreateRepository(handler);

            await repo.LoadIfStaleAsync();
            var current = await repo.GetCurrentAsync();

            Assert.AreEqual(new DateOnly(2024, 1, 1), current!.EffectiveDate);
        }

        [TestMethod]
        public async Task LoadIfStaleAsync_ConcurrentCalls_FetchesIndexOnlyOnce()
        {
            SeedCache(new DateOnly(2020, 1, 1), ageDays: 30, (999, BanlistLimit.Forbidden));
            var indexCallCount = 0;
            var handler = new FakeHttpMessageHandler(request =>
            {
                var url = request.RequestUri!.ToString();
                if (url.EndsWith("/index", StringComparison.Ordinal))
                {
                    Interlocked.Increment(ref indexCallCount);
                    return JsonResponse(JsonConvert.SerializeObject(new[] { new { name = "2024-01-01.vector.json" } }));
                }

                return JsonResponse(BuildListJson("2024-01-01", (100, 0)));
            });
            var repo = CreateRepository(handler);

            await Task.WhenAll(repo.LoadIfStaleAsync(), repo.LoadIfStaleAsync());

            Assert.AreEqual(1, indexCallCount);
        }

        [TestMethod]
        public async Task LoadIfStaleAsync_DatedListReturnsMalformedJson_KeepsExistingCache()
        {
            SeedCache(new DateOnly(2020, 1, 1), ageDays: 30, (999, BanlistLimit.Forbidden));
            var handler = BuildHandler(
                indexNames: ["2024-01-01.vector.json"],
                currentJson: BuildListJson("2024-01-01", (200, 1)),
                listJsonByFileName: new Dictionary<string, string> { ["2024-01-01.vector.json"] = "{ not valid json" });
            var repo = CreateRepository(handler);

            await repo.LoadIfStaleAsync();
            var current = await repo.GetCurrentAsync();

            Assert.AreEqual(new DateOnly(2020, 1, 1), current!.EffectiveDate);
        }

        [TestMethod]
        public async Task LoadIfStaleAsync_OneDatedListFetchFails_KeepsExistingCache()
        {
            SeedCache(new DateOnly(2020, 1, 1), ageDays: 30, (999, BanlistLimit.Forbidden));
            var handler = BuildHandler(
                indexNames: ["2023-01-01.vector.json", "2024-01-01.vector.json"],
                currentJson: BuildListJson("2024-01-01", (200, 1)),
                // "2023-01-01.vector.json" is deliberately absent, so it 404s.
                listJsonByFileName: new Dictionary<string, string> { ["2024-01-01.vector.json"] = BuildListJson("2024-01-01", (200, 1)) });
            var repo = CreateRepository(handler);

            await repo.LoadIfStaleAsync();
            var current = await repo.GetCurrentAsync();

            Assert.AreEqual(new DateOnly(2020, 1, 1), current!.EffectiveDate);
        }

        [TestMethod]
        public async Task LoadIfStaleAsync_WarmCacheButFetchFails_KeepsServingStaleCache()
        {
            SeedCache(new DateOnly(2020, 1, 1), ageDays: 30, (999, BanlistLimit.Forbidden));
            var repo = CreateRepository(BuildHandler(indexFails: true));

            await repo.LoadIfStaleAsync();
            var current = await repo.GetCurrentAsync();

            Assert.IsNotNull(current);
            Assert.AreEqual(new DateOnly(2020, 1, 1), current!.EffectiveDate);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            if (Directory.Exists(_cacheDir))
                Directory.Delete(_cacheDir, recursive: true);
        }

        [TestInitialize]
        public void TestInitialize()
        {
            _cacheDir = Path.Combine(Path.GetTempPath(), "BanlistRepositoryTests_" + Guid.NewGuid().ToString("N"));
        }
        private static FakeHttpMessageHandler BuildHandler(
            IReadOnlyList<string>? indexNames = null,
            string? currentJson = null,
            IReadOnlyDictionary<string, string>? listJsonByFileName = null,
            bool indexFails = false) =>
            new(request =>
            {
                var url = request.RequestUri!.ToString();

                if (url.EndsWith("/index", StringComparison.Ordinal))
                {
                    return indexFails
                        ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                        : JsonResponse(JsonConvert.SerializeObject((indexNames ?? []).Select(n => new { name = n })));
                }

                if (url.EndsWith("current.vector.json", StringComparison.Ordinal))
                {
                    return currentJson is null
                        ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                        : JsonResponse(currentJson);
                }

                var fileName = url.Split('/').Last();
                return listJsonByFileName is not null && listJsonByFileName.TryGetValue(fileName, out var json)
                    ? JsonResponse(json)
                    : new HttpResponseMessage(HttpStatusCode.NotFound);
            });

        private static string BuildListJson(string date, params (int KonamiID, int Limit)[] entries) =>
            JsonConvert.SerializeObject(new
            {
                date,
                regulation = entries.ToDictionary(e => e.KonamiID.ToString(), e => e.Limit)
            });

        private static HttpResponseMessage JsonResponse(string json) =>
            new(HttpStatusCode.OK) { Content = new StringContent(json) };

        private BanlistRepository CreateRepository(HttpMessageHandler handler, int cacheTtlDays = 7)
        {
            var httpClient = new HttpClient(handler);
            var factory = new Mock<IHttpClientFactory>();
            factory.Setup(f => f.CreateClient("YamlYugiLimitRegulation")).Returns(httpClient);

            var settings = new BanlistSettings
            {
                BaseUrl = "https://fake.example/",
                CacheTtlDays = cacheTtlDays,
                IndexUrl = "https://fake.example/index"
            };

            return new BanlistRepository(new Mock<ILogger<BanlistRepository>>().Object, factory.Object, Options.Create(settings), _cacheDir);
        }
        // Writes in the on-disk cache shape BanlistRepository.WriteCache produces (Current/Lists of Banlist,
        // not the raw {date, regulation} vector-file shape BuildListJson fakes for HTTP responses).
        private void SeedCache(DateOnly effectiveDate, int ageDays, params (int KonamiID, BanlistLimit Limit)[] limits)
        {
            Directory.CreateDirectory(_cacheDir);
            var banlist = new Banlist
            {
                EffectiveDate = effectiveDate,
                LimitsByKonamiID = limits.ToDictionary(e => e.KonamiID, e => e.Limit)
            };
            var root = new { Current = banlist, Lists = new[] { banlist } };
            File.WriteAllText(Path.Combine(_cacheDir, "banlistcache.json"), JsonConvert.SerializeObject(root));
            File.WriteAllText(Path.Combine(_cacheDir, "banlistcache.json.timestamp"), DateTime.UtcNow.AddDays(-ageDays).ToString("O"));
        }
    }
}
