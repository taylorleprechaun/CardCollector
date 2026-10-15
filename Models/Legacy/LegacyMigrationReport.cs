using System.Globalization;
using System.Text;
using CardCollector.Data.Models.Catalog;

namespace CardCollector.Models.Legacy
{
    /// <summary>The outcome of migrating a legacy database: how the catalog was built and what happened to each table's rows.</summary>
    public sealed class LegacyMigrationReport
    {
        public CatalogSyncRun? CatalogSync { get; set; }

        public DateTime FinishedUTC { get; set; }

        public string LegacyDatabasePath { get; init; } = string.Empty;

        /// <summary>How the copied value snapshots were linked to catalog cards; null until that step runs.</summary>
        public LegacySnapshotCardFill? SnapshotCardFill { get; set; }

        public DateTime StartedUTC { get; init; }

        public IList<LegacyTableReport> Tables { get; } = new List<LegacyTableReport>();

        public IList<LegacyUnmappedRow> UnmappedRows { get; } = new List<LegacyUnmappedRow>();

        /// <summary>A plain-text report for the console and the report file.</summary>
        public string ToText()
        {
            var text = new StringBuilder();
            text.AppendLine(CultureInfo.InvariantCulture, $"Legacy migration from {LegacyDatabasePath}");
            text.AppendLine(CultureInfo.InvariantCulture, $"Started {StartedUTC:yyyy-MM-dd HH:mm:ss} UTC, finished {FinishedUTC:yyyy-MM-dd HH:mm:ss} UTC");
            if (CatalogSync is not null)
                text.AppendLine(CultureInfo.InvariantCulture,
                    $"Catalog: {CatalogSync.Status}, {CatalogSync.CardsAdded} cards, {CatalogSync.PrintingsAdded} printings, {CatalogSync.ProductsLinked} TCGPlayer products linked, {CatalogSync.ProductsUnmapped} unmapped");
            text.AppendLine();

            text.AppendLine(CultureInfo.InvariantCulture, $"{"Table",-32}{"Read",9}{"Written",9}{"Exact",9}{"Card fix",9}{"Variant",9}{"Merged",9}{"Unmapped",10}");
            foreach (var table in Tables)
                text.AppendLine(CultureInfo.InvariantCulture,
                    $"{table.Table,-32}{table.Read,9}{table.Written,9}{table.Exact,9}{table.CardCorrected,9}{table.VariantFilled,9}{table.Merged,9}{table.Unmapped,10}");

            if (SnapshotCardFill is { } fill)
            {
                text.AppendLine();
                text.AppendLine(CultureInfo.InvariantCulture,
                    $"Value snapshots linked to a catalog card: {fill.ByEntry} through their entry, {fill.ByName} by card name, {fill.Unlinked} not linked.");
            }
            var unaccounted = Tables.Where(t => t.Read != t.Written + t.Merged + t.Unmapped).Select(t => t.Table).ToList();
            text.AppendLine();
            text.AppendLine(unaccounted.Count == 0
                ? "Every legacy row is accounted for (written, merged into another row, or in the review inbox)."
                : $"WARNING: rows unaccounted for in {string.Join(", ", unaccounted)}.");

            if (UnmappedRows.Count > 0)
            {
                text.AppendLine();
                text.AppendLine(CultureInfo.InvariantCulture, $"Unmapped rows ({UnmappedRows.Count}), each now a review item:");
                foreach (var row in UnmappedRows)
                    text.AppendLine(CultureInfo.InvariantCulture, $"  {row.Table} #{row.ID}: {row.Description} — {row.Reason}");
            }

            return text.ToString();
        }
    }
}
