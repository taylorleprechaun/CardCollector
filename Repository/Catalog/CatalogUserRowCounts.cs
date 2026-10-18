using System.Linq.Expressions;
using CardCollector.Data;
using CardCollector.Data.Models.Catalog;
using Microsoft.EntityFrameworkCore;

namespace CardCollector.Repository.Catalog
{
    /// <summary>The one query the catalog repositories use to see which printings the user's own rows still point at.</summary>
    public static class CatalogUserRowCounts
    {
        /// <summary>
        /// Counts the collection, wishlist, checked-out, and cart rows on each printing <paramref name="printings"/> selects;
        /// printings with none are left out.
        /// </summary>
        public static async Task<IReadOnlyDictionary<int, int>> CountUserRowsByPrintingAsync(
            this AppDBContext context,
            Expression<Func<CatalogPrinting, bool>> printings,
            CancellationToken cancellationToken = default)
        {
            if (context is null) throw new ArgumentNullException(nameof(context));
            if (printings is null) throw new ArgumentNullException(nameof(printings));

            var printingIDs = context.CatalogPrintings.Where(printings).Select(p => p.ID);
            var rows = new List<int>();
            rows.AddRange(await context.CollectionEntries.AsNoTracking()
                .Where(e => printingIDs.Contains(e.CatalogPrintingID))
                .Select(e => e.CatalogPrintingID)
                .ToListAsync(cancellationToken).ConfigureAwait(false));
            rows.AddRange(await context.PreferredVersions.AsNoTracking()
                .Where(v => printingIDs.Contains(v.CatalogPrintingID))
                .Select(v => v.CatalogPrintingID)
                .ToListAsync(cancellationToken).ConfigureAwait(false));
            rows.AddRange(await context.CheckedOutCards.AsNoTracking()
                .Where(c => printingIDs.Contains(c.CatalogPrintingID))
                .Select(c => c.CatalogPrintingID)
                .ToListAsync(cancellationToken).ConfigureAwait(false));
            rows.AddRange(await context.PendingOrderLines.AsNoTracking()
                .Where(l => printingIDs.Contains(l.CatalogPrintingID))
                .Select(l => l.CatalogPrintingID)
                .ToListAsync(cancellationToken).ConfigureAwait(false));

            return rows.GroupBy(id => id).ToDictionary(g => g.Key, g => g.Count());
        }
    }
}
