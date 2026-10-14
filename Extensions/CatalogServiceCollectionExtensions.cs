using CardCollector.Models.Catalog;
using CardCollector.Repository;
using CardCollector.Repository.Catalog;
using CardCollector.Repository.Legacy;
using CardCollector.Services;

namespace CardCollector.Extensions
{
    public static class CatalogServiceCollectionExtensions
    {
        public static IServiceCollection AddCatalogModule(this IServiceCollection services, IConfiguration configuration)
        {
            if (configuration is null) throw new ArgumentNullException(nameof(configuration));

            services.Configure<CatalogSyncSettings>(configuration.GetSection("CatalogSync"));

            services.AddSingleton<ICardDataRepository, CatalogCardDataRepository>();
            services.AddScoped<ICatalogAdminRepository, CatalogAdminRepository>();
            services.AddScoped<ICatalogAdminService, CatalogAdminService>();
            services.AddScoped<ICatalogRepository, CatalogRepository>();
            services.AddScoped<ICatalogReviewRepository, CatalogReviewRepository>();
            services.AddScoped<ICatalogReviewService, CatalogReviewService>();
            services.AddScoped<ICatalogSyncService, CatalogSyncService>();
            services.AddSingleton<ILegacyDatabaseReader, LegacyDatabaseReader>();
            services.AddScoped<ILegacyImportRepository, LegacyImportRepository>();
            services.AddScoped<ILegacyMigrationService, LegacyMigrationService>();

            return services;
        }
    }
}
