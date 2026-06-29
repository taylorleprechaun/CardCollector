using CardCollector.Data;
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
builder.Services.AddScoped<ICheckedOutRepository, CheckedOutRepository>();
builder.Services.AddScoped<ICollectionRepository, CollectionRepository>();
builder.Services.AddScoped<ICollectionEntryValueRepository, CollectionEntryValueRepository>();
builder.Services.AddScoped<ICollectionValueRepository, CollectionValueRepository>();
builder.Services.AddScoped<IDismissedNewPrintingRepository, DismissedNewPrintingRepository>();
builder.Services.AddScoped<IPreferredVersionRepository, PreferredVersionRepository>();
builder.Services.AddScoped<IPricingService, PricingService>();
builder.Services.AddScoped<ICardService, CardService>();

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

app.MapGet("/api/price", async (int cardID, string setCode, string rarityName, IPricingService pricingService) =>
{
    var price = await pricingService.GetPrintingPriceAsync(cardID, setCode, rarityName);
    return Results.Json(new { price });
});

app.MapPost("/api/stats/calculate-value", async (ICardService cardService) =>
{
    var (totalValue, cardCount, setValueBreakdown, topValueCards) = await cardService.CalculateCurrentMarketValueAsync();
    return Results.Json(new
    {
        totalValue,
        cardCount,
        setValueLabels = setValueBreakdown.Select(x => x.Label).ToArray(),
        setValueData = setValueBreakdown.Select(x => x.Value).ToArray(),
        topCards = topValueCards.Select(x => new { cardName = x.CardName, setName = x.SetName, rarityName = x.RarityName, value = x.Value }).ToArray()
    });
});

app.MapGet("/api/stats/calculate-value/stream", async (ICardService cardService, HttpContext ctx, CancellationToken ct) =>
{
    ctx.Response.ContentType = "text/event-stream";
    ctx.Response.Headers.CacheControl = "no-cache";
    ctx.Response.Headers.Connection = "keep-alive";

    async Task Send(string eventName, string data)
    {
        await ctx.Response.WriteAsync($"event: {eventName}\ndata: {data}\n\n", ct);
        await ctx.Response.Body.FlushAsync(ct);
    }

    var (totalValue, cardCount, setValueBreakdown, topValueCards) =
        await cardService.CalculateCurrentMarketValueAsync(async (current, total) =>
            await Send("progress", $"{{\"current\":{current},\"total\":{total}}}"));

    var completeJson = JsonSerializer.Serialize(new
    {
        totalValue,
        cardCount,
        setValueLabels = setValueBreakdown.Select(x => x.Label).ToArray(),
        setValueData = setValueBreakdown.Select(x => x.Value).ToArray(),
        topCards = topValueCards.Select(x => new { cardName = x.CardName, setName = x.SetName, rarityName = x.RarityName, value = x.Value }).ToArray()
    });
    await Send("complete", completeJson);
});

app.MapGet("/api/stats/smart-refresh/stream", async (ICardService cardService, HttpContext ctx, CancellationToken ct) =>
{
    ctx.Response.ContentType = "text/event-stream";
    ctx.Response.Headers.CacheControl = "no-cache";
    ctx.Response.Headers.Connection = "keep-alive";

    async Task Send(string eventName, string data)
    {
        await ctx.Response.WriteAsync($"event: {eventName}\ndata: {data}\n\n", ct);
        await ctx.Response.Body.FlushAsync(ct);
    }

    var (totalValue, cardCount, setValueBreakdown, topValueCards) =
        await cardService.CalculateSmartMarketValueAsync(async (current, total) =>
            await Send("progress", $"{{\"current\":{current},\"total\":{total}}}"));

    var completeJson = JsonSerializer.Serialize(new
    {
        totalValue,
        cardCount,
        setValueLabels = setValueBreakdown.Select(x => x.Label).ToArray(),
        setValueData = setValueBreakdown.Select(x => x.Value).ToArray(),
        topCards = topValueCards.Select(x => new { cardName = x.CardName, setName = x.SetName, rarityName = x.RarityName, value = x.Value }).ToArray()
    });
    await Send("complete", completeJson);
});

app.MapGet("/api/stats/card-price-history", async (string cardName, ICardService cardService) =>
{
    if (string.IsNullOrWhiteSpace(cardName))
        return Results.BadRequest("cardName is required.");
    var history = await cardService.GetCardPriceHistoryAsync(cardName);
    return Results.Json(history.Select(s => new { label = s.Label, dates = s.Dates, values = s.Values }));
});

app.Run();
