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
builder.Services.AddScoped<ICardService, CardService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    var columns = db.Database
        .SqlQueryRaw<string>("SELECT name FROM pragma_table_info('CollectionEntries')")
        .ToList();

    if (!columns.Contains("AcquisitionMethod"))
        db.Database.ExecuteSqlRaw("ALTER TABLE CollectionEntries ADD COLUMN AcquisitionMethod TEXT NULL");

    if (!columns.Contains("IsPlaceholder"))
        db.Database.ExecuteSqlRaw("ALTER TABLE CollectionEntries ADD COLUMN IsPlaceholder INTEGER NOT NULL DEFAULT 0");

    if (!columns.Contains("Quantity"))
    {
        db.Database.ExecuteSqlRaw("ALTER TABLE CollectionEntries ADD COLUMN Quantity INTEGER NOT NULL DEFAULT 1");
        db.Database.ExecuteSqlRaw("UPDATE CollectionEntries SET Quantity = 3");
        db.Database.ExecuteSqlRaw("DROP INDEX IF EXISTS IX_CollectionEntries_ImageID_SetCode");
    }
}

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();

app.Run();
