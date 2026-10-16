using CardCollector.Data;
using CardCollector.Extensions;
using CardCollector.Models.Catalog;
using CardCollector.Repository;
using CardCollector.Repository.Sources;
using CardCollector.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

// A maintenance command (e.g. catalog-sync) runs against its own database and exits instead of starting the server.
var catalogCommand = CardCollector.CatalogCommandLine.Parse(args);
catalogCommand?.PrepareDatabase();
var builder = WebApplication.CreateBuilder(catalogCommand is null ? args : []);

builder.Configuration.AddJsonFile("appsettings-private.json", optional: true, reloadOnChange: true);
if (catalogCommand is not null)
{
    builder.Configuration["ConnectionStrings:DefaultConnection"] = catalogCommand.ConnectionString;
    builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
}

builder.Services.AddAuthentication("CardCollectorCookie")
    .AddCookie("CardCollectorCookie", options =>
    {
        options.LoginPath = "/Login";
        options.Cookie.Name = builder.Configuration["Auth:CookieName"];
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.ExpireTimeSpan = TimeSpan.FromHours(
            builder.Configuration.GetValue<int>("Auth:CookieExpirationHours"));
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToPage("/Error");
    options.Conventions.AllowAnonymousToPage("/Login");
});
builder.Services.AddHttpClient("YGOProDeck", client =>
{
    client.BaseAddress = new Uri("https://db.ygoprodeck.com/");
    client.ApplyAppDefaults(TimeSpan.FromSeconds(120));
});
builder.Services.AddHttpClient("TcgCsv", client =>
{
    client.BaseAddress = new Uri("https://tcgcsv.com/");
    client.ApplyAppDefaults(TimeSpan.FromSeconds(120));
});
builder.Services.AddDbContext<AppDBContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddSingleton<ICardSetRepository, CardSetRepository>();
builder.Services.AddSingleton<IPricingDataCache, PricingDataCache>();
builder.Services.AddSingleton<IRazorPartialRenderer, RazorPartialRenderer>();
builder.Services.AddSingleton<ITCGCatalogCache, TCGCatalogCache>();
builder.Services.AddSingleton<IYamlYugiSource, YamlYugiSource>();
builder.Services.AddSingleton<IYGOProDeckSource, YGOProDeckSource>();
builder.Services.AddScoped<ICheckedOutRepository, CheckedOutRepository>();
builder.Services.AddScoped<ICollectionRepository, CollectionRepository>();
builder.Services.AddScoped<ICollectionEntryValueRepository, CollectionEntryValueRepository>();
builder.Services.AddScoped<ICollectionValueRepository, CollectionValueRepository>();
builder.Services.AddScoped<IDismissedNewPrintingRepository, DismissedNewPrintingRepository>();
builder.Services.AddScoped<IIgnoredCardRepository, IgnoredCardRepository>();
builder.Services.AddScoped<IPendingOrderRepository, PendingOrderRepository>();
builder.Services.AddScoped<IPreferredVersionRepository, PreferredVersionRepository>();
builder.Services.AddScoped<IPricingService, PricingService>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IWishlistValueRepository, WishlistValueRepository>();
builder.Services.AddScoped<ICardService, CardService>();
builder.Services.AddCatalogModule(builder.Configuration);
builder.Services.AddTournamentsModule(builder.Configuration);
builder.Services.AddHostedService<CatalogWarmupHostedService>();
builder.Services.AddHostedService<PriceRefreshBackgroundService>();

var app = builder.Build();

// The first start after the catalog cutover finds no database at the configured path but finds the legacy one: build the
// new database from it before anything opens the database. The legacy file is only read, so it stays the rollback.
if (catalogCommand is null)
{
    var databasePath = new SqliteConnectionStringBuilder(builder.Configuration.GetConnectionString("DefaultConnection")).DataSource;
    var legacyDatabasePath = builder.Configuration.GetValue<string>("LegacyMigration:LegacyDatabasePath");
    CardCollector.LegacyMigrationRunner.DiscardIncompleteMigration(databasePath, app.Services.GetRequiredService<ILogger<Program>>());
    if (!File.Exists(databasePath) && !string.IsNullOrWhiteSpace(legacyDatabasePath) && File.Exists(legacyDatabasePath))
    {
        var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
        startupLogger.LogWarning("No database at {DatabasePath}; migrating the legacy database {LegacyDatabasePath}", databasePath, legacyDatabasePath);
        await CardCollector.LegacyMigrationRunner.RunAsync(app.Services, legacyDatabasePath, databasePath, CatalogSourceMode.IfStale, startupLogger);
    }
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDBContext>();
    db.Database.EnsureCreated();
}

if (catalogCommand is not null)
{
    Environment.ExitCode = await catalogCommand.RunAsync(app.Services);
    return;
}

await app.Services.GetRequiredService<ICardDataRepository>().ReloadAsync();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

app.MapGet("/api/price", CardCollector.APIEndpoints.GetPriceAsync).RequireAuthorization();

app.MapGet("/api/stats/card-price-history", CardCollector.APIEndpoints.GetCardPriceHistoryAsync).RequireAuthorization();

app.Run();
