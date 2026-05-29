using CardCollector.Data;
using CardCollector.Repository;
using CardCollector.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddSingleton<ICardDataRepository, CardDataRepository>();
builder.Services.AddScoped<ICollectionRepository, CollectionRepository>();
builder.Services.AddScoped<IPreferredVersionRepository, PreferredVersionRepository>();
builder.Services.AddScoped<ICardService, CardService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    // Create PreferredVersions table for existing databases (EnsureCreated won't alter existing schemas)
    await db.Database.ExecuteSqlRawAsync(@"
        CREATE TABLE IF NOT EXISTS PreferredVersions (
            ID INTEGER PRIMARY KEY AUTOINCREMENT,
            CardID INTEGER NOT NULL,
            ImageID INTEGER NOT NULL UNIQUE,
            SetCode TEXT NOT NULL,
            DateCreated TEXT NOT NULL,
            DateModified TEXT NOT NULL
        )");

    // Migrate non-placeholder collection entries to preferred versions (idempotent via INSERT OR IGNORE)
    await db.Database.ExecuteSqlRawAsync(@"
        INSERT OR IGNORE INTO PreferredVersions (CardID, ImageID, SetCode, DateCreated, DateModified)
        SELECT CardID, ImageID, SetCode, datetime('now'), datetime('now')
        FROM CollectionEntries
        WHERE IsPlaceholder = 0
          AND ID IN (
            SELECT MIN(ID)
            FROM CollectionEntries
            WHERE IsPlaceholder = 0
            GROUP BY ImageID
          )");
}

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();

app.Run();
