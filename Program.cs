using CardCollector.Data;
using CardCollector.DTO;
using CardCollector.Extensions;
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
    client.ApplyAppDefaults(TimeSpan.FromSeconds(120));
});
builder.Services.AddHttpClient("TcgCsv", client =>
{
    client.BaseAddress = new Uri("https://tcgcsv.com/");
    client.ApplyAppDefaults(TimeSpan.FromSeconds(120));
});
builder.Services.AddDbContext<AppDBContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddSingleton<ICardDataRepository, CardDataRepository>();
builder.Services.AddSingleton<ICardSetRepository, CardSetRepository>();
builder.Services.AddSingleton<IPricingDataCache, PricingDataCache>();
builder.Services.AddSingleton<IRazorPartialRenderer, RazorPartialRenderer>();
builder.Services.AddSingleton<ITCGCatalogCache, TCGCatalogCache>();
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
builder.Services.AddTournamentsModule(builder.Configuration);
builder.Services.AddHostedService<CatalogWarmupHostedService>();
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
            "SetCode" TEXT NOT NULL,
            "RarityName" TEXT NULL,
            "PrintVariant" TEXT NULL,
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

    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "WishlistValueSnapshots" (
            "ID" INTEGER NOT NULL CONSTRAINT "PK_WishlistValueSnapshots" PRIMARY KEY AUTOINCREMENT,
            "DateCreated" TEXT NOT NULL,
            "RemainingCount" INTEGER NOT NULL,
            "SnapshotDate" TEXT NOT NULL,
            "TotalValue" REAL NOT NULL,
            CONSTRAINT "UQ_WishlistValueSnapshots_SnapshotDate" UNIQUE ("SnapshotDate")
        );
        """);

    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    var preferredVersionColumns = await db.Database
        .SqlQueryRaw<string>("SELECT name FROM pragma_table_info('PreferredVersions')")
        .ToListAsync();
    if (!preferredVersionColumns.Contains("DesiredQuantity"))
    {
        db.Database.ExecuteSqlRaw("""
            ALTER TABLE "PreferredVersions" ADD COLUMN "DesiredQuantity" INTEGER NOT NULL DEFAULT 3;
            """);
    }

    // The live table was originally created with an inline `ImageID INTEGER NOT NULL UNIQUE` column
    // constraint (a sqlite_autoindex, not a standalone named index), so `DROP INDEX` can't remove it —
    // SQLite requires rebuilding the table to drop a UNIQUE column constraint. Detect it by origin='u'
    // (created by a UNIQUE constraint, as opposed to origin='c' for an explicit CREATE INDEX) rather
    // than by name, since the exact autoindex name is SQLite-assigned and not something we control.
    var legacyUniqueConstraintIndexNames = await db.Database
        .SqlQueryRaw<string>("""SELECT name FROM pragma_index_list('PreferredVersions') WHERE "unique" = 1 AND origin = 'u'""")
        .ToListAsync();
    var hasLegacyImageIDUniqueConstraint = false;
    foreach (var indexName in legacyUniqueConstraintIndexNames)
    {
        // indexName comes from pragma_index_list (SQLite's own catalog), never user input.
#pragma warning disable EF1002
        var columns = await db.Database
            .SqlQueryRaw<string>($"SELECT name FROM pragma_index_info('{indexName}')")
            .ToListAsync();
#pragma warning restore EF1002
        if (columns is ["ImageID"])
        {
            hasLegacyImageIDUniqueConstraint = true;
            break;
        }
    }

    if (hasLegacyImageIDUniqueConstraint)
    {
        using var transaction = db.Database.BeginTransaction();
        db.Database.ExecuteSqlRaw("""
            CREATE TABLE "PreferredVersions_new" (
                "ID" INTEGER NOT NULL CONSTRAINT "PK_PreferredVersions" PRIMARY KEY AUTOINCREMENT,
                "CardID" INTEGER NOT NULL,
                "ImageID" INTEGER NOT NULL,
                "SetCode" TEXT NOT NULL,
                "DateCreated" TEXT NOT NULL,
                "DateModified" TEXT NOT NULL,
                "RarityName" TEXT NULL,
                "DesiredQuantity" INTEGER NOT NULL DEFAULT 3
            );
            """);
        db.Database.ExecuteSqlRaw("""
            INSERT INTO "PreferredVersions_new" ("ID", "CardID", "ImageID", "SetCode", "DateCreated", "DateModified", "RarityName", "DesiredQuantity")
            SELECT "ID", "CardID", "ImageID", "SetCode", "DateCreated", "DateModified", "RarityName", "DesiredQuantity" FROM "PreferredVersions";
            """);
        db.Database.ExecuteSqlRaw("""DROP TABLE "PreferredVersions";""");
        db.Database.ExecuteSqlRaw("""ALTER TABLE "PreferredVersions_new" RENAME TO "PreferredVersions";""");
        transaction.Commit();
    }

    // ImageID used to be the collection-tracking unit (one row per artwork), but that model was abandoned
    // in favor of card-level tracking — the UI has not let a user pick a specific artwork since. ImageID is
    // now retired from every tracking table in favor of (CardID, SetCode, RarityName, PrintVariant); the
    // display thumbnail is derived from CardID's primary artwork at render time instead of being stored per
    // row. SQLite can't drop a column that's part of an index/constraint directly, so each table is rebuilt —
    // same pattern as the PreferredVersions rebuild above. Guarded by column-existence check so this is a
    // no-op on a database that already went through it (or was EnsureCreated fresh from the current model).
    async Task<bool> HasColumnAsync(string table, string column)
    {
        var columns = await db.Database.SqlQuery<string>($"SELECT name FROM pragma_table_info({table})").ToListAsync();
        return columns.Contains(column);
    }

    if (await HasColumnAsync("CollectionEntries", "ImageID"))
    {
        using var transaction = db.Database.BeginTransaction();
        db.Database.ExecuteSqlRaw("""
            CREATE TABLE "CollectionEntries_new" (
                "ID" INTEGER NOT NULL CONSTRAINT "PK_CollectionEntries" PRIMARY KEY AUTOINCREMENT,
                "CardID" INTEGER NOT NULL,
                "SetCode" TEXT NOT NULL,
                "Status" TEXT NOT NULL,
                "Condition" TEXT NULL,
                "Edition" TEXT NULL,
                "PurchaseDate" TEXT NULL,
                "PurchasePrice" TEXT NULL,
                "DateCreated" TEXT NOT NULL,
                "DateModified" TEXT NOT NULL,
                "AcquisitionMethod" TEXT NULL,
                "IsPlaceholder" INTEGER NOT NULL DEFAULT 0,
                "Quantity" INTEGER NOT NULL DEFAULT 1,
                "MarketPriceAtEntry" REAL NULL,
                "RarityName" TEXT NULL,
                "PrintVariant" TEXT NULL
            );
            """);
        db.Database.ExecuteSqlRaw("""
            INSERT INTO "CollectionEntries_new" ("ID", "CardID", "SetCode", "Status", "Condition", "Edition", "PurchaseDate", "PurchasePrice", "DateCreated", "DateModified", "AcquisitionMethod", "IsPlaceholder", "Quantity", "MarketPriceAtEntry", "RarityName")
            SELECT "ID", "CardID", "SetCode", "Status", "Condition", "Edition", "PurchaseDate", "PurchasePrice", "DateCreated", "DateModified", "AcquisitionMethod", "IsPlaceholder", "Quantity", "MarketPriceAtEntry", "RarityName" FROM "CollectionEntries";
            """);
        db.Database.ExecuteSqlRaw("""DROP TABLE "CollectionEntries";""");
        db.Database.ExecuteSqlRaw("""ALTER TABLE "CollectionEntries_new" RENAME TO "CollectionEntries";""");
        transaction.Commit();
    }
    db.Database.ExecuteSqlRaw("""DROP INDEX IF EXISTS "IX_CollectionEntries_ImageID_SetCode";""");
    db.Database.ExecuteSqlRaw("""
        CREATE INDEX IF NOT EXISTS "IX_CollectionEntries_CardID_SetCode" ON "CollectionEntries" ("CardID", "SetCode");
        """);

    if (await HasColumnAsync("PreferredVersions", "ImageID"))
    {
        using var transaction = db.Database.BeginTransaction();
        db.Database.ExecuteSqlRaw("""
            CREATE TABLE "PreferredVersions_new2" (
                "ID" INTEGER NOT NULL CONSTRAINT "PK_PreferredVersions" PRIMARY KEY AUTOINCREMENT,
                "CardID" INTEGER NOT NULL,
                "SetCode" TEXT NOT NULL,
                "DateCreated" TEXT NOT NULL,
                "DateModified" TEXT NOT NULL,
                "RarityName" TEXT NULL,
                "DesiredQuantity" INTEGER NOT NULL DEFAULT 3,
                "PrintVariant" TEXT NULL
            );
            """);
        db.Database.ExecuteSqlRaw("""
            INSERT INTO "PreferredVersions_new2" ("ID", "CardID", "SetCode", "DateCreated", "DateModified", "RarityName", "DesiredQuantity")
            SELECT "ID", "CardID", "SetCode", "DateCreated", "DateModified", "RarityName", "DesiredQuantity" FROM "PreferredVersions";
            """);
        db.Database.ExecuteSqlRaw("""DROP TABLE "PreferredVersions";""");
        db.Database.ExecuteSqlRaw("""ALTER TABLE "PreferredVersions_new2" RENAME TO "PreferredVersions";""");
        transaction.Commit();
    }

    if (await HasColumnAsync("CheckedOutCards", "ImageID"))
    {
        using var transaction = db.Database.BeginTransaction();
        db.Database.ExecuteSqlRaw("""
            CREATE TABLE "CheckedOutCards_new" (
                "ID" INTEGER NOT NULL CONSTRAINT "PK_CheckedOutCards" PRIMARY KEY AUTOINCREMENT,
                "CardID" INTEGER NOT NULL,
                "CheckedOutDate" TEXT NOT NULL,
                "DateCreated" TEXT NOT NULL,
                "DateModified" TEXT NOT NULL,
                "SetCode" TEXT NOT NULL,
                "Quantity" INTEGER NOT NULL DEFAULT 1,
                "RarityName" TEXT NOT NULL DEFAULT '',
                "PrintVariant" TEXT NULL
            );
            """);
        db.Database.ExecuteSqlRaw("""
            INSERT INTO "CheckedOutCards_new" ("ID", "CardID", "CheckedOutDate", "DateCreated", "DateModified", "SetCode", "Quantity", "RarityName")
            SELECT "ID", "CardID", "CheckedOutDate", "DateCreated", "DateModified", "SetCode", "Quantity", "RarityName" FROM "CheckedOutCards";
            """);
        db.Database.ExecuteSqlRaw("""DROP TABLE "CheckedOutCards";""");
        db.Database.ExecuteSqlRaw("""ALTER TABLE "CheckedOutCards_new" RENAME TO "CheckedOutCards";""");
        transaction.Commit();
    }

    if (await HasColumnAsync("PendingOrderLines", "ImageID"))
    {
        using var transaction = db.Database.BeginTransaction();
        db.Database.ExecuteSqlRaw("""
            CREATE TABLE "PendingOrderLines_new" (
                "ID" INTEGER NOT NULL CONSTRAINT "PK_PendingOrderLines" PRIMARY KEY AUTOINCREMENT,
                "CardID" INTEGER NOT NULL,
                "SetCode" TEXT NOT NULL,
                "RarityName" TEXT NULL,
                "PrintVariant" TEXT NULL,
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
        db.Database.ExecuteSqlRaw("""
            INSERT INTO "PendingOrderLines_new" ("ID", "CardID", "SetCode", "RarityName", "Condition", "Edition", "AcquisitionMethod", "PurchaseDate", "PurchasePrice", "MarketPriceAtEntry", "Quantity", "DateCreated")
            SELECT "ID", "CardID", "SetCode", "RarityName", "Condition", "Edition", "AcquisitionMethod", "PurchaseDate", "PurchasePrice", "MarketPriceAtEntry", "Quantity", "DateCreated" FROM "PendingOrderLines";
            """);
        db.Database.ExecuteSqlRaw("""DROP TABLE "PendingOrderLines";""");
        db.Database.ExecuteSqlRaw("""ALTER TABLE "PendingOrderLines_new" RENAME TO "PendingOrderLines";""");
        transaction.Commit();
    }

    // DismissedNewPrintings' unique constraint was declared inline (CONSTRAINT ... UNIQUE (...)), which
    // SQLite backs with an anonymous sqlite_autoindex that DROP INDEX can't remove — same restriction as
    // the legacy PreferredVersions rebuild above — so widening it to include PrintVariant needs a rebuild too.
    if (!await HasColumnAsync("DismissedNewPrintings", "PrintVariant"))
    {
        using var transaction = db.Database.BeginTransaction();
        db.Database.ExecuteSqlRaw("""
            CREATE TABLE "DismissedNewPrintings_new" (
                "ID" INTEGER NOT NULL CONSTRAINT "PK_DismissedNewPrintings" PRIMARY KEY AUTOINCREMENT,
                "CardID" INTEGER NOT NULL,
                "SetCode" TEXT NOT NULL,
                "RarityName" TEXT NOT NULL,
                "PrintVariant" TEXT NULL,
                "DateCreated" TEXT NOT NULL,
                "DateModified" TEXT NOT NULL,
                CONSTRAINT "UQ_DismissedNewPrintings_CardID_SetCode_RarityName_PrintVariant" UNIQUE ("CardID", "SetCode", "RarityName", "PrintVariant")
            );
            """);
        db.Database.ExecuteSqlRaw("""
            INSERT INTO "DismissedNewPrintings_new" ("ID", "CardID", "SetCode", "RarityName", "DateCreated", "DateModified")
            SELECT "ID", "CardID", "SetCode", "RarityName", "DateCreated", "DateModified" FROM "DismissedNewPrintings";
            """);
        db.Database.ExecuteSqlRaw("""DROP TABLE "DismissedNewPrintings";""");
        db.Database.ExecuteSqlRaw("""ALTER TABLE "DismissedNewPrintings_new" RENAME TO "DismissedNewPrintings";""");
        transaction.Commit();
    }

    // PreferredVersions used to allow only one tracked printing per card, enforced only at the app
    // level (AddOrUpdateAsync matched by CardID alone) rather than by the DB, so historical data can
    // already contain rows that share a (CardID, SetCode, RarityName) — normalize rarity first, then
    // collapse any resulting duplicates, before the new unique index below is built over this table.
    foreach (var preferredVersion in db.PreferredVersions.Where(p => p.RarityName != null))
        preferredVersion.RarityName = RarityExtensions.NormalizeRarityName(preferredVersion.RarityName);
    db.SaveChanges();

    foreach (var group in db.PreferredVersions.ToList()
        .GroupBy(p => (p.CardID, p.SetCode, p.RarityName)))
    {
        var duplicates = group.OrderBy(p => p.ID).Skip(1).ToList();
        foreach (var duplicate in duplicates)
            logger.LogWarning(
                "Removing duplicate PreferredVersions row {ID} (CardID={CardID}, SetCode={SetCode}, RarityName={RarityName}) — superseded by row {WinnerID}.",
                duplicate.ID, duplicate.CardID, duplicate.SetCode, duplicate.RarityName, group.OrderBy(p => p.ID).First().ID);
        db.PreferredVersions.RemoveRange(duplicates);
    }
    db.SaveChanges();

    // PreferredVersions used to allow only one tracked printing per card, enforced by a unique index
    // on ImageID (the artwork, shared across all set/rarity printings of that artwork since the source
    // data has no per-set artwork mapping). Multiple printings of the same card can now be tracked
    // independently, and PrintVariant distinguishes same-rarity print variants (e.g. Extended Art), so
    // the uniqueness moves to (CardID, SetCode, RarityName, PrintVariant).
    db.Database.ExecuteSqlRaw("""DROP INDEX IF EXISTS "IX_PreferredVersions_ImageID";""");
    db.Database.ExecuteSqlRaw("""DROP INDEX IF EXISTS "IX_PreferredVersions_CardID_SetCode_RarityName";""");
    db.Database.ExecuteSqlRaw("""
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_PreferredVersions_CardID_SetCode_RarityName_PrintVariant"
        ON "PreferredVersions" ("CardID", "SetCode", "RarityName", "PrintVariant");
        """);

    // Some providers label plain Common cards as "Short Print"/"Super Short Print" instead
    // (see RarityExtensions.NormalizeRarityName). Collapse any historical data written before
    // the override existed. Idempotent: rows already normalized are left untouched on every
    // subsequent startup.
    foreach (var entry in db.CollectionEntries.Where(e => e.RarityName != null))
        entry.RarityName = RarityExtensions.NormalizeRarityName(entry.RarityName);

    foreach (var line in db.PendingOrderLines.Where(l => l.RarityName != null))
        line.RarityName = RarityExtensions.NormalizeRarityName(line.RarityName);

    foreach (var snapshot in db.CollectionEntryValueSnapshots.Where(s => s.RarityName != null))
        snapshot.RarityName = RarityExtensions.NormalizeRarityName(snapshot.RarityName) ?? snapshot.RarityName;

    // CheckedOutCards and DismissedNewPrintings both have a unique index that includes RarityName,
    // so normalizing a row can collide with an existing row that's already "Common" for the same
    // key. When that happens, keep the existing row and drop the now-redundant duplicate.
    foreach (var group in db.CheckedOutCards.ToList()
        .GroupBy(c => (c.CardID, c.SetCode, Normalized: RarityExtensions.NormalizeRarityName(c.RarityName))))
    {
        var winner = group.OrderBy(c => c.RarityName == group.Key.Normalized ? 0 : 1).First();
        winner.RarityName = group.Key.Normalized ?? winner.RarityName;

        var duplicates = group.Where(c => c.ID != winner.ID).ToList();
        foreach (var duplicate in duplicates)
            logger.LogWarning(
                "Removing duplicate CheckedOutCards row {ID} (CardID={CardID}, SetCode={SetCode}, RarityName={RarityName}) — superseded by row {WinnerID} after rarity normalization.",
                duplicate.ID, duplicate.CardID, duplicate.SetCode, duplicate.RarityName, winner.ID);
        db.CheckedOutCards.RemoveRange(duplicates);
    }
    db.SaveChanges();

    // CheckedOutCards used to be keyed by ImageID (the artwork); now that ImageID is retired,
    // uniqueness moves to (CardID, SetCode, RarityName, PrintVariant).
    db.Database.ExecuteSqlRaw("""DROP INDEX IF EXISTS "IX_CheckedOutCards_ImageID_SetCode_RarityName";""");
    db.Database.ExecuteSqlRaw("""
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_CheckedOutCards_CardID_SetCode_RarityName_PrintVariant"
        ON "CheckedOutCards" ("CardID", "SetCode", "RarityName", "PrintVariant");
        """);

    foreach (var group in db.DismissedNewPrintings.ToList()
        .GroupBy(d => (d.CardID, d.SetCode, Normalized: RarityExtensions.NormalizeRarityName(d.RarityName), d.PrintVariant)))
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

    TournamentSchema.Apply(db);
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

app.Run();
