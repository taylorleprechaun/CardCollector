using CardCollector.Repository;
using CardCollector.Services;

namespace CardCollector.Extensions
{
    public static class TournamentsServiceCollectionExtensions
    {
        public static IServiceCollection AddTournamentsModule(this IServiceCollection services)
        {
            services.AddScoped<IEventRepository, EventRepository>();
            services.AddScoped<IEventService, EventService>();
            services.AddScoped<IFormatRepository, FormatRepository>();
            services.AddScoped<IFormatService, FormatService>();

            return services;
        }
    }
}
