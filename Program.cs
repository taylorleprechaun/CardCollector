using CardCollector.Data;
using CardCollector.Repository;
using CardCollector.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
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
    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS ""CheckedOutCards"" (
            ""ID""             INTEGER NOT NULL CONSTRAINT ""PK_CheckedOutCards"" PRIMARY KEY AUTOINCREMENT,
            ""CardID""         INTEGER NOT NULL,
            ""CheckedOutDate"" TEXT    NOT NULL,
            ""DateCreated""    TEXT    NOT NULL,
            ""DateModified""   TEXT    NOT NULL,
            ""ImageID""        INTEGER NOT NULL,
            ""SetCode""        TEXT    NOT NULL,
            ""Quantity""       INTEGER NOT NULL DEFAULT 1
        );
        CREATE UNIQUE INDEX IF NOT EXISTS ""IX_CheckedOutCards_ImageID_SetCode""
            ON ""CheckedOutCards"" (""ImageID"", ""SetCode"");
    ");
    var existingCols = db.Database
        .SqlQuery<string>($"SELECT name FROM pragma_table_info('CheckedOutCards')")
        .ToList();
    if (!existingCols.Contains("Quantity"))
        db.Database.ExecuteSqlRaw(@"ALTER TABLE ""CheckedOutCards"" ADD COLUMN ""Quantity"" INTEGER NOT NULL DEFAULT 1");
}

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseRouting();
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

app.Run();
