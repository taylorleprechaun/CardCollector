using Microsoft.EntityFrameworkCore;

namespace CardCollector.Data
{
    /// <summary>
    /// Gives user rows saved without a <c>CatalogPrintingID</c> (before <see cref="PrintingKeySync"/> existed) the ID of
    /// the printing their (card, set code, rarity, variant) columns name. Idempotent; temporary, like the columns.
    /// </summary>
    public static class PrintingKeyBackfill
    {
        private static readonly IReadOnlyList<string> Tables =
            ["CheckedOutCards", "CollectionEntries", "DismissedNewPrintings", "PendingOrderLines", "PreferredVersions"];

        /// <summary>Fills every missing printing ID it can and returns how many rows it filled.</summary>
        public static int Apply(AppDBContext db)
        {
            if (db is null) throw new ArgumentNullException(nameof(db));

            var filled = 0;
            foreach (var table in Tables)
            {
                var sql = $"""
                    UPDATE "{table}" SET "CatalogPrintingID" = (
                        SELECT p."ID" FROM "CatalogPrintings" p
                        WHERE p."CatalogCardID" = "{table}"."CardID"
                          AND p."SetCode" = "{table}"."SetCode"
                          AND p."RarityName" = "{table}"."RarityName"
                          AND p."PrintVariant" IS "{table}"."PrintVariant")
                    WHERE "CatalogPrintingID" IS NULL AND EXISTS (
                        SELECT 1 FROM "CatalogPrintings" p
                        WHERE p."CatalogCardID" = "{table}"."CardID"
                          AND p."SetCode" = "{table}"."SetCode"
                          AND p."RarityName" = "{table}"."RarityName"
                          AND p."PrintVariant" IS "{table}"."PrintVariant");
                    """;
                filled += db.Database.ExecuteSqlRaw(sql);
            }

            return filled;
        }
    }
}
