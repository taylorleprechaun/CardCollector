using CardCollector.Repository;
using CardCollector.Services;

namespace CardCollector.Extensions
{
    public static class TournamentsServiceCollectionExtensions
    {
        public static IServiceCollection AddTournamentsModule(this IServiceCollection services, IConfiguration configuration)
        {
            if (configuration is null) throw new ArgumentNullException(nameof(configuration));

            services.AddHttpClient("YamlYugiLimitRegulation", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(60);
                client.DefaultRequestHeaders.Add("User-Agent", "CardCollector/1.0 (personal Yu-Gi-Oh collection tracker)");
            });
            services.Configure<BanlistSettings>(configuration.GetSection("BanlistSettings"));

            services.AddScoped<IAnalyticsService, AnalyticsService>();
            services.AddSingleton<IBanlistRepository, BanlistRepository>();
            services.AddScoped<IDeckLegalityService, DeckLegalityService>();
            services.AddScoped<IDeckRepository, DeckRepository>();
            services.AddScoped<IDeckService, DeckService>();
            services.AddScoped<IEventRepository, EventRepository>();
            services.AddScoped<IEventService, EventService>();
            services.AddScoped<IFormatRepository, FormatRepository>();
            services.AddScoped<IFormatService, FormatService>();
            services.AddScoped<IMatchRepository, MatchRepository>();
            services.AddScoped<IMatchService, MatchService>();

            return services;
        }
    }
}
