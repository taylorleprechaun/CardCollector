using CardCollector.Data;
using CardCollector.Extensions;
using CardCollector.Repository;
using CardCollector.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;

namespace CardCollector.Tests.Extensions
{
    [TestClass]
    public sealed class TournamentsServiceCollectionExtensionsTests
    {
        [TestMethod]
        public void AddTournamentsModule_BanlistSettingsSection_BindsSettings()
        {
            using var provider = BuildProvider(new Dictionary<string, string?>
            {
                ["BanlistSettings:BaseUrl"] = "https://example.test/lists/",
                ["BanlistSettings:CacheTtlDays"] = "3"
            });

            var settings = provider.GetRequiredService<IOptions<BanlistSettings>>().Value;

            Assert.AreEqual("https://example.test/lists/", settings.BaseUrl);
            Assert.AreEqual(3, settings.CacheTtlDays);
        }

        [TestMethod]
        public void AddTournamentsModule_NullConfiguration_ThrowsArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => new ServiceCollection().AddTournamentsModule(null!));
        }

        [TestMethod]
        [DataRow(typeof(IAnalyticsService), ServiceLifetime.Scoped, DisplayName = "Analytics service")]
        [DataRow(typeof(IBanlistRepository), ServiceLifetime.Singleton, DisplayName = "Banlist repository")]
        [DataRow(typeof(IDeckLegalityService), ServiceLifetime.Scoped, DisplayName = "Deck legality service")]
        [DataRow(typeof(IDeckRepository), ServiceLifetime.Scoped, DisplayName = "Deck repository")]
        [DataRow(typeof(IDeckService), ServiceLifetime.Scoped, DisplayName = "Deck service")]
        [DataRow(typeof(IEventRepository), ServiceLifetime.Scoped, DisplayName = "Event repository")]
        [DataRow(typeof(IEventService), ServiceLifetime.Scoped, DisplayName = "Event service")]
        [DataRow(typeof(IFormatRepository), ServiceLifetime.Scoped, DisplayName = "Format repository")]
        [DataRow(typeof(IFormatService), ServiceLifetime.Scoped, DisplayName = "Format service")]
        [DataRow(typeof(IMatchRepository), ServiceLifetime.Scoped, DisplayName = "Match repository")]
        [DataRow(typeof(IMatchService), ServiceLifetime.Scoped, DisplayName = "Match service")]
        public void AddTournamentsModule_Registration_UsesExpectedLifetime(Type serviceType, ServiceLifetime expected)
        {
            var services = new ServiceCollection().AddTournamentsModule(new ConfigurationBuilder().Build());

            var descriptor = services.Single(d => d.ServiceType == serviceType);

            Assert.AreEqual(expected, descriptor.Lifetime);
        }

        [TestMethod]
        [DataRow(typeof(IAnalyticsService), DisplayName = "Analytics service")]
        [DataRow(typeof(IBanlistRepository), DisplayName = "Banlist repository")]
        [DataRow(typeof(IDeckLegalityService), DisplayName = "Deck legality service")]
        [DataRow(typeof(IDeckRepository), DisplayName = "Deck repository")]
        [DataRow(typeof(IDeckService), DisplayName = "Deck service")]
        [DataRow(typeof(IEventRepository), DisplayName = "Event repository")]
        [DataRow(typeof(IEventService), DisplayName = "Event service")]
        [DataRow(typeof(IFormatRepository), DisplayName = "Format repository")]
        [DataRow(typeof(IFormatService), DisplayName = "Format service")]
        [DataRow(typeof(IMatchRepository), DisplayName = "Match repository")]
        [DataRow(typeof(IMatchService), DisplayName = "Match service")]
        public void AddTournamentsModule_ScopeValidationOn_ResolvesService(Type serviceType)
        {
            using var provider = BuildProvider();
            using var scope = provider.CreateScope();

            var service = scope.ServiceProvider.GetService(serviceType);

            Assert.IsNotNull(service);
        }
        /// <summary>
        /// Registers what the module expects the app to provide, then builds with scope validation on, so a
        /// scoped dependency captured by a singleton fails the build.
        /// </summary>
        private static ServiceProvider BuildProvider(IDictionary<string, string?>? settings = null)
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings ?? new Dictionary<string, string?>()).Build();
            var databaseName = Guid.NewGuid().ToString();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<AppDBContext>(options => options.UseInMemoryDatabase(databaseName));
            services.AddSingleton(new Mock<ICardDataRepository>().Object);
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddTournamentsModule(configuration);

            return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        }
    }
}
