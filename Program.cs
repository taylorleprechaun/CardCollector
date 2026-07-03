using CardCollector.Data;
using CardCollector.Data.Models;
using CardCollector.Repository;
using CardCollector.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

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
builder.Services.AddScoped<IPreferredVersionRepository, PreferredVersionRepository>();
builder.Services.AddScoped<IPricingService, PricingService>();
builder.Services.AddScoped<ICardService, CardService>();
builder.Services.AddHostedService<PriceRefreshBackgroundService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDBContext>();
    db.Database.EnsureCreated();
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

app.MapGet("/api/price", async (int cardID, string setCode, string rarityName, string? edition, IPricingService pricingService) =>
{
    CardEdition? parsedEdition = Enum.TryParse<CardEdition>(edition, out var e) ? e : null;
    var price = await pricingService.GetPrintingPriceAsync(cardID, setCode, rarityName, parsedEdition);
    return Results.Json(new { price });
});

app.MapGet("/api/stats/card-price-history", async (string cardName, ICardService cardService) =>
{
    if (string.IsNullOrWhiteSpace(cardName))
        return Results.BadRequest("cardName is required.");
    var history = await cardService.GetCardPriceHistoryAsync(cardName);
    return Results.Json(history.Select(s => new { label = s.Label, dates = s.Dates, values = s.Values }));
});

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

    await Send("start", "{}");
    await cardDataRepository.RefreshAsync();
    var count = cardDataRepository.GetBrowseableCards().Count();
    await Send("complete", JsonSerializer.Serialize(new { cardCount = count }));
});

app.MapPost("/api/admin/refresh-pricing-data", async (IPricingDataCache pricingDataCache) =>
{
    await pricingDataCache.RefreshAsync();
    return Results.Ok();
});

app.Run();
