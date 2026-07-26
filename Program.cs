using CardCollector.Data;
using CardCollector.DTO;
using CardCollector.Repository;
using CardCollector.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings-private.json", optional: true, reloadOnChange: true);

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
    options.Conventions.AllowAnonymousToPage("/Login");
});
builder.Services.AddHttpClient("YGOProDeck", client =>
{
    client.BaseAddress = new Uri("https://db.ygoprodeck.com/");
    client.Timeout = TimeSpan.FromSeconds(120);
    client.DefaultRequestHeaders.Add("User-Agent", "CardCollector/1.0");
});
builder.Services.AddDbContext<AppDBContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddSingleton<ICardDataRepository, CardDataRepository>();
builder.Services.AddSingleton<ICardSetRepository, CardSetRepository>();
builder.Services.AddSingleton<IPricingDataCache, PricingDataCache>();
builder.Services.AddSingleton<IRazorPartialRenderer, RazorPartialRenderer>();
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
builder.Services.AddScoped<ICardService, CardService>();
builder.Services.AddHostedService<PriceRefreshBackgroundService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDBContext>();
    db.Database.EnsureCreated();

    // EnsureCreated() only builds the schema for a brand-new database file, so tables added after the
    // database already existed (like this one) need to be created here on every startup instead.
    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "IgnoredCards" (
            "ID" INTEGER NOT NULL CONSTRAINT "PK_IgnoredCards" PRIMARY KEY AUTOINCREMENT,
            "CardID" INTEGER NOT NULL,
            "DateCreated" TEXT NOT NULL,
            "DateModified" TEXT NOT NULL,
            CONSTRAINT "UQ_IgnoredCards_CardID" UNIQUE ("CardID")
        );
        """);

    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "PendingOrderLines" (
            "ID" INTEGER NOT NULL CONSTRAINT "PK_PendingOrderLines" PRIMARY KEY AUTOINCREMENT,
            "CardID" INTEGER NOT NULL,
            "ImageID" INTEGER NOT NULL,
            "SetCode" TEXT NOT NULL,
            "RarityName" TEXT NULL,
            "Condition" TEXT NULL,
            "Edition" TEXT NULL,
            "AcquisitionMethod" TEXT NULL,
            "PurchaseDate" TEXT NULL,
            "PurchasePrice" REAL NULL,
            "MarketPriceAtEntry" REAL NULL,
            "Quantity" INTEGER NOT NULL DEFAULT 1,
            "DateCreated" TEXT NOT NULL
        );
        """);

    // Some providers label plain Common cards as "Short Print"/"Super Short Print" instead
    // (see RarityExtensions.NormalizeRarityName). Collapse any historical data written before
    // the override existed. Idempotent: rows already normalized are left untouched on every
    // subsequent startup.
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    foreach (var entry in db.CollectionEntries.Where(e => e.RarityName != null))
        entry.RarityName = RarityExtensions.NormalizeRarityName(entry.RarityName);

    foreach (var preferredVersion in db.PreferredVersions.Where(p => p.RarityName != null))
        preferredVersion.RarityName = RarityExtensions.NormalizeRarityName(preferredVersion.RarityName);

    foreach (var line in db.PendingOrderLines.Where(l => l.RarityName != null))
        line.RarityName = RarityExtensions.NormalizeRarityName(line.RarityName);

    foreach (var snapshot in db.CollectionEntryValueSnapshots.Where(s => s.RarityName != null))
        snapshot.RarityName = RarityExtensions.NormalizeRarityName(snapshot.RarityName) ?? snapshot.RarityName;

    // CheckedOutCards and DismissedNewPrintings both have a unique index that includes RarityName,
    // so normalizing a row can collide with an existing row that's already "Common" for the same
    // key. When that happens, keep the existing row and drop the now-redundant duplicate.
    foreach (var group in db.CheckedOutCards.ToList()
        .GroupBy(c => (c.ImageID, c.SetCode, Normalized: RarityExtensions.NormalizeRarityName(c.RarityName))))
    {
        var winner = group.OrderBy(c => c.RarityName == group.Key.Normalized ? 0 : 1).First();
        winner.RarityName = group.Key.Normalized ?? winner.RarityName;

        var duplicates = group.Where(c => c.ID != winner.ID).ToList();
        foreach (var duplicate in duplicates)
            logger.LogWarning(
                "Removing duplicate CheckedOutCards row {ID} (ImageID={ImageID}, SetCode={SetCode}, RarityName={RarityName}) — superseded by row {WinnerID} after rarity normalization.",
                duplicate.ID, duplicate.ImageID, duplicate.SetCode, duplicate.RarityName, winner.ID);
        db.CheckedOutCards.RemoveRange(duplicates);
    }

    foreach (var group in db.DismissedNewPrintings.ToList()
        .GroupBy(d => (d.CardID, d.SetCode, Normalized: RarityExtensions.NormalizeRarityName(d.RarityName))))
    {
        var winner = group.OrderBy(d => d.RarityName == group.Key.Normalized ? 0 : 1).First();
        winner.RarityName = group.Key.Normalized ?? winner.RarityName;

        var duplicates = group.Where(d => d.ID != winner.ID).ToList();
        foreach (var duplicate in duplicates)
            logger.LogWarning(
                "Removing duplicate DismissedNewPrintings row {ID} (CardID={CardID}, SetCode={SetCode}, RarityName={RarityName}) — superseded by row {WinnerID} after rarity normalization.",
                duplicate.ID, duplicate.CardID, duplicate.SetCode, duplicate.RarityName, winner.ID);
        db.DismissedNewPrintings.RemoveRange(duplicates);
    }

    db.SaveChanges();
}

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

app.MapGet("/api/price", CardCollector.APIEndpoints.GetPriceAsync);

app.MapGet("/api/stats/card-price-history", CardCollector.APIEndpoints.GetCardPriceHistoryAsync);

app.MapGet("/api/admin/refresh-card-data/stream", async (ICardDataRepository cardDataRepository, HttpContext ctx, CancellationToken ct) =>
{
    ctx.Response.ContentType = "text/event-stream";
    ctx.Response.Headers.CacheControl = "no-cache";
    ctx.Response.Headers.Connection = "keep-alive";

    async Task Send(string eventName, string data)
    {
        await ctx.Response.WriteAsync($"event: {eventName}\ndata: {data}\n\n", ct);
        await ctx.Response.Body.FlushAsync(ct);
    }

    await CardCollector.APIEndpoints.RefreshCardDataStreamAsync(cardDataRepository, Send, ct);
});

app.MapPost("/api/admin/refresh-pricing-data", CardCollector.APIEndpoints.RefreshPricingDataAsync);

app.Run();
