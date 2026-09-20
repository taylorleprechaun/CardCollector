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

            // Events.DeckID is a plain nullable column with no foreign key: SQLite cannot add a constraint to an
            // existing column later, so the link to a deck is enforced in code instead.
            db.Database.ExecuteSqlRaw("""
                CREATE TABLE IF NOT EXISTS "Events" (
                    "ID" INTEGER NOT NULL CONSTRAINT "PK_Events" PRIMARY KEY AUTOINCREMENT,
                    "Date" TEXT NOT NULL,
                    "DateCreated" TEXT NOT NULL,
                    "DateModified" TEXT NOT NULL,
                    "DeckID" INTEGER NULL,
                    "DeckName" TEXT NOT NULL,
                    "DecklistURL" TEXT NULL,
                    "EventType" TEXT NOT NULL,
                    "Finish" INTEGER NULL,
                    "FinishNote" TEXT NULL,
                    "Location" TEXT NOT NULL,
                    "Notes" TEXT NULL,
                    "Players" INTEGER NULL,
                    "TopCut" TEXT NULL
                );
                """);

            db.Database.ExecuteSqlRaw("""
                CREATE TABLE IF NOT EXISTS "Matches" (
                    "ID" INTEGER NOT NULL CONSTRAINT "PK_Matches" PRIMARY KEY AUTOINCREMENT,
                    "DateCreated" TEXT NOT NULL,
                    "DateModified" TEXT NOT NULL,
                    "EventID" INTEGER NOT NULL,
                    "GamesLost" INTEGER NOT NULL,
                    "GamesTied" INTEGER NOT NULL,
                    "GamesWon" INTEGER NOT NULL,
                    "IsBye" INTEGER NOT NULL,
                    "Notes" TEXT NULL,
                    "OpponentDeck" TEXT NOT NULL,
                    "Result" TEXT NOT NULL,
                    "Round" TEXT NOT NULL,
                    "Sequence" INTEGER NOT NULL,
                    "WonDiceRoll" INTEGER NULL,
                    CONSTRAINT "FK_Matches_Events_EventID" FOREIGN KEY ("EventID") REFERENCES "Events" ("ID") ON DELETE CASCADE
                );
                """);

            db.Database.ExecuteSqlRaw("""
                CREATE INDEX IF NOT EXISTS "IX_Events_Date" ON "Events" ("Date");
                """);

            db.Database.ExecuteSqlRaw("""
                CREATE INDEX IF NOT EXISTS "IX_Matches_EventID" ON "Matches" ("EventID");
                """);
        }
    }
}
