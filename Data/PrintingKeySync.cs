using CardCollector.Data.Models;
using CardCollector.Data.Models.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace CardCollector.Data
{
    /// <summary>
    /// Keeps a user row's <see cref="IPrintingKeyedRow.CatalogPrintingID"/> and its (card, set code, rarity, variant) columns
    /// pointing at the same printing whenever rows are saved.
    /// </summary>
    /// <remarks>
    /// While pages move from the columns to the ID one area at a time, a row may be written by either kind of code and read
    /// by the other. A row whose ID was set or changed gets its columns from that printing; a row whose columns changed (or
    /// that has no ID) gets the ID of the printing with exactly those columns, or none if no printing matches. Temporary:
    /// removed together with the columns.
    /// </remarks>
    internal static class PrintingKeySync
    {
        public static void Apply(DbContext context)
        {
            var plan = Plan(context);
            if (plan.IsEmpty)
                return;

            Fill(plan, Query(context, plan).ToList());
        }

        public static async Task ApplyAsync(DbContext context, CancellationToken cancellationToken)
        {
            var plan = Plan(context);
            if (plan.IsEmpty)
                return;

            Fill(plan, await Query(context, plan).ToListAsync(cancellationToken).ConfigureAwait(false));
        }

        private static void Fill(PrintingKeySyncPlan plan, IReadOnlyList<CatalogPrinting> printings)
        {
            var byID = printings.ToDictionary(p => p.ID);
            foreach (var row in plan.FromID)
            {
                if (!byID.TryGetValue(row.CatalogPrintingID!.Value, out var printing))
                    continue;

                row.CardID = printing.CatalogCardID;
                row.SetCode = printing.SetCode;
                row.RarityName = printing.RarityName;
                row.PrintVariant = printing.PrintVariant;
            }

            foreach (var row in plan.FromColumns)
            {
                row.CatalogPrintingID = printings.FirstOrDefault(p =>
                    p.CatalogCardID == row.CardID
                    && string.Equals(p.SetCode, row.SetCode, StringComparison.Ordinal)
                    && string.Equals(p.RarityName, row.RarityName, StringComparison.Ordinal)
                    && string.Equals(p.PrintVariant, row.PrintVariant, StringComparison.Ordinal))?.ID;
            }
        }

        private static bool IsModified(EntityEntry entry, string propertyName) => entry.Property(propertyName).IsModified;

        private static PrintingKeySyncPlan Plan(DbContext context)
        {
            var plan = new PrintingKeySyncPlan();
            foreach (var entry in context.ChangeTracker.Entries<IPrintingKeyedRow>())
            {
                if (entry.State is not (EntityState.Added or EntityState.Modified))
                    continue;

                var row = entry.Entity;
                var idWritten = row.CatalogPrintingID is not null
                    && (entry.State == EntityState.Added || IsModified(entry, nameof(IPrintingKeyedRow.CatalogPrintingID)));
                var columnsWritten = entry.State == EntityState.Modified
                    && (IsModified(entry, nameof(IPrintingKeyedRow.CardID))
                        || IsModified(entry, nameof(IPrintingKeyedRow.SetCode))
                        || IsModified(entry, nameof(IPrintingKeyedRow.RarityName))
                        || IsModified(entry, nameof(IPrintingKeyedRow.PrintVariant)));

                if (idWritten)
                    plan.FromID.Add(row);
                else if (row.CatalogPrintingID is null || columnsWritten)
                    plan.FromColumns.Add(row);
            }

            return plan;
        }

        /// <summary>One query for every printing the plan needs: by ID, plus every printing of the cards matched by columns.</summary>
        private static IQueryable<CatalogPrinting> Query(DbContext context, PrintingKeySyncPlan plan)
        {
            var ids = plan.FromID.Select(r => r.CatalogPrintingID!.Value).Distinct().ToList();
            var cardIDs = plan.FromColumns.Select(r => r.CardID).Distinct().ToList();
            return context.Set<CatalogPrinting>()
                .AsNoTracking()
                .Where(p => ids.Contains(p.ID) || cardIDs.Contains(p.CatalogCardID));
        }
    }
}
