using Microsoft.EntityFrameworkCore;

namespace CardCollector.Data
{
    /// <summary>
    /// Creates the Tournaments tables on databases that predate them. <c>EnsureCreated()</c> only builds
    /// the schema for a brand-new database file, so every statement here is idempotent and must match
    /// the EF model exactly.
    /// </summary>
    public static class TournamentSchema
    {
        public static void Apply(AppDBContext db)
        {
            if (db is null) throw new ArgumentNullException(nameof(db));

            db.Database.ExecuteSqlRaw("""
                CREATE TABLE IF NOT EXISTS "Formats" (
                    "ID" INTEGER NOT NULL CONSTRAINT "PK_Formats" PRIMARY KEY AUTOINCREMENT,
                    "DateCreated" TEXT NOT NULL,
                    "DateModified" TEXT NOT NULL,
                    "EndDate" TEXT NULL,
                    "Name" TEXT NOT NULL,
                    "Notes" TEXT NULL,
                    "StartDate" TEXT NOT NULL
                );
                """);

            db.Database.ExecuteSqlRaw("""
                CREATE TABLE IF NOT EXISTS "FormatStrategies" (
                    "ID" INTEGER NOT NULL CONSTRAINT "PK_FormatStrategies" PRIMARY KEY AUTOINCREMENT,
                    "FormatID" INTEGER NOT NULL,
                    "Name" TEXT NOT NULL,
                    "Position" INTEGER NOT NULL,
                    CONSTRAINT "FK_FormatStrategies_Formats_FormatID" FOREIGN KEY ("FormatID") REFERENCES "Formats" ("ID") ON DELETE CASCADE
                );
                """);

            db.Database.ExecuteSqlRaw("""
                CREATE INDEX IF NOT EXISTS "IX_FormatStrategies_FormatID" ON "FormatStrategies" ("FormatID");
                """);
        }
    }
}
