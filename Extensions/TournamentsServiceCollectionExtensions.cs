using CardCollector.Repository;
using CardCollector.Services;

namespace CardCollector.Extensions
{
    public static class TournamentsServiceCollectionExtensions
    {
        public static IServiceCollection AddTournamentsModule(this IServiceCollection services)
        {
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
