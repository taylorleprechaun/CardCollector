using System.Globalization;
using CardCollector.Data;
using CardCollector.Data.Models.Catalog;
using CardCollector.Models.Catalog;
using Microsoft.EntityFrameworkCore;

namespace CardCollector.Repository.Catalog
{
    public sealed class CatalogAdminRepository : ICatalogAdminRepository
    {
        private const string LIKE_ESCAPE = @"\";

        private readonly AppDBContext _context;

        public CatalogAdminRepository(AppDBContext context)
        {
            _context = context;
        }

        public void AddSet(CatalogSet set) => _context.CatalogSets.Add(set);

        public async Task<IReadOnlyDictionary<int, int>> CountUserRowsByPrintingAsync(int catalogCardID, CancellationToken cancellationToken = default)
        {
            var printingIDs = new List<int>();
            printingIDs.AddRange(await _context.CollectionEntries.AsNoTracking()
                .Where(e => e.CatalogPrinting!.CatalogCardID == catalogCardID)
                .Select(e => e.CatalogPrintingID)
                .ToListAsync(cancellationToken).ConfigureAwait(false));
            printingIDs.AddRange(await _context.PreferredVersions.AsNoTracking()
                .Where(v => v.CatalogPrinting!.CatalogCardID == catalogCardID)
                .Select(v => v.CatalogPrintingID)
                .ToListAsync(cancellationToken).ConfigureAwait(false));
            printingIDs.AddRange(await _context.CheckedOutCards.AsNoTracking()
                .Where(c => c.CatalogPrinting!.CatalogCardID == catalogCardID)
                .Select(c => c.CatalogPrintingID)
                .ToListAsync(cancellationToken).ConfigureAwait(false));
            printingIDs.AddRange(await _context.PendingOrderLines.AsNoTracking()
                .Where(l => l.CatalogPrinting!.CatalogCardID == catalogCardID)
                .Select(l => l.CatalogPrintingID)
                .ToListAsync(cancellationToken).ConfigureAwait(false));

            return printingIDs.GroupBy(id => id).ToDictionary(g => g.Key, g => g.Count());
        }

        public async Task<CatalogCard?> FindOtherCardWithPasscodeAsync(int passcode, int exceptCardID, CancellationToken cancellationToken = default) =>
            await _context.CatalogCards
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Passcode == passcode && c.ID != exceptCardID && c.Status == CatalogStatus.Active, cancellationToken)
                .ConfigureAwait(false);

        public async Task<CatalogCard?> GetCardAsync(int catalogCardID, CancellationToken cancellationToken = default) =>
            await _context.CatalogCards
                .Include(c => c.Aliases)
                .Include(c => c.Images)
                .Include(c => c.Printings)
                    .ThenInclude(p => p.Sources)
                .Include(c => c.Printings)
                    .ThenInclude(p => p.Set)
                .AsSplitQuery()
                .FirstOrDefaultAsync(c => c.ID == catalogCardID, cancellationToken)
                .ConfigureAwait(false);

        public async Task<CatalogPrinting?> GetPrintingAsync(int catalogPrintingID, CancellationToken cancellationToken = default) =>
            await _context.CatalogPrintings
                .Include(p => p.Card)
                .Include(p => p.Sources)
                .Include(p => p.Set)
                .FirstOrDefaultAsync(p => p.ID == catalogPrintingID, cancellationToken)
                .ConfigureAwait(false);

        public async Task<CatalogPrintingSource?> GetPrintingSourceAsync(int id, CancellationToken cancellationToken = default) =>
            await _context.CatalogPrintingSources.FirstOrDefaultAsync(s => s.ID == id, cancellationToken).ConfigureAwait(false);

        public async Task<CatalogReviewItem?> GetReviewItemAsync(CatalogReviewItemKind kind, string subjectKey, CancellationToken cancellationToken = default) =>
            await _context.CatalogReviewItems
                .FirstOrDefaultAsync(i => i.Kind == kind && i.SubjectKey == subjectKey, cancellationToken)
                .ConfigureAwait(false);

        public async Task<CatalogSet?> GetSetByPrefixAsync(string prefix, CancellationToken cancellationToken = default) =>
            await _context.CatalogSets.FirstOrDefaultAsync(s => s.Prefix == prefix, cancellationToken).ConfigureAwait(false);

        public async Task<(IReadOnlyList<CatalogSyncRun> Items, int TotalCount)> GetSyncRunsAsync(int page, int pageSize, CancellationToken cancellationToken = default)
        {
            var totalCount = await _context.CatalogSyncRuns.CountAsync(cancellationToken).ConfigureAwait(false);
            var items = await _context.CatalogSyncRuns
                .AsNoTracking()
                .OrderByDescending(r => r.StartedUTC)
                .ThenByDescending(r => r.ID)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return (items, totalCount);
        }

        public async Task<CatalogUserRowMoveResult> MoveUserRowsAsync(CatalogPrinting from, CatalogPrinting into, CancellationToken cancellationToken = default)
        {
            if (from is null) throw new ArgumentNullException(nameof(from));
            if (into is null) throw new ArgumentNullException(nameof(into));

            var (fromID, intoID) = (from.ID, into.ID);
            var moved = 0;
            var folded = 0;

            foreach (var entry in await _context.CollectionEntries
                .Where(e => e.CatalogPrintingID == fromID)
                .ToListAsync(cancellationToken).ConfigureAwait(false))
            {
                entry.CatalogPrintingID = intoID;
                moved++;
            }

            foreach (var line in await _context.PendingOrderLines
                .Where(l => l.CatalogPrintingID == fromID)
                .ToListAsync(cancellationToken).ConfigureAwait(false))
            {
                line.CatalogPrintingID = intoID;
                moved++;
            }

            var keptVersion = await _context.PreferredVersions
                .FirstOrDefaultAsync(v => v.CatalogPrintingID == intoID, cancellationToken)
                .ConfigureAwait(false);
            foreach (var version in await _context.PreferredVersions
                .Where(v => v.CatalogPrintingID == fromID)
                .ToListAsync(cancellationToken).ConfigureAwait(false))
            {
                if (keptVersion is not null)
                {
                    keptVersion.DesiredQuantity = Math.Max(keptVersion.DesiredQuantity, version.DesiredQuantity);
                    _context.PreferredVersions.Remove(version);
                    folded++;
                    continue;
                }

                version.CatalogPrintingID = intoID;
                keptVersion = version;
                moved++;
            }

            var keptCheckout = await _context.CheckedOutCards
                .FirstOrDefaultAsync(c => c.CatalogPrintingID == intoID, cancellationToken)
                .ConfigureAwait(false);
            foreach (var checkout in await _context.CheckedOutCards
                .Where(c => c.CatalogPrintingID == fromID)
                .ToListAsync(cancellationToken).ConfigureAwait(false))
            {
                if (keptCheckout is not null)
                {
                    keptCheckout.Quantity += checkout.Quantity;
                    _context.CheckedOutCards.Remove(checkout);
                    folded++;
                    continue;
                }

                checkout.CatalogPrintingID = intoID;
                keptCheckout = checkout;
                moved++;
            }

            var keptDismissal = await _context.DismissedNewPrintings
                .FirstOrDefaultAsync(d => d.CatalogPrintingID == intoID, cancellationToken)
                .ConfigureAwait(false);
            foreach (var dismissal in await _context.DismissedNewPrintings
                .Where(d => d.CatalogPrintingID == fromID)
                .ToListAsync(cancellationToken).ConfigureAwait(false))
            {
                if (keptDismissal is not null)
                {
                    _context.DismissedNewPrintings.Remove(dismissal);
                    folded++;
                    continue;
                }

                dismissal.CatalogPrintingID = intoID;
                keptDismissal = dismissal;
                moved++;
            }

            return new CatalogUserRowMoveResult(moved, folded);
        }

        public void RemovePrintingSource(CatalogPrintingSource source) => _context.CatalogPrintingSources.Remove(source);

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        public async Task<(IReadOnlyList<CatalogCard> Items, int TotalCount)> SearchCardsAsync(
            string? query,
            CatalogStatus? status,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            var cards = _context.CatalogCards.AsNoTracking();
            if (status is CatalogStatus only)
                cards = cards.Where(c => c.Status == only);

            var text = query?.Trim();
            if (!string.IsNullOrEmpty(text))
            {
                if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
                {
                    cards = cards.Where(c => c.ID == number || c.KonamiID == number || c.Passcode == number || c.Aliases.Any(a => a.Value == number));
                }
                else
                {
                    var escaped = text.Replace(@"\", @"\\", StringComparison.Ordinal)
                        .Replace("%", @"\%", StringComparison.Ordinal)
                        .Replace("_", @"\_", StringComparison.Ordinal);
                    var namePattern = $"%{escaped}%";
                    var codePattern = $"{escaped}%";
                    cards = cards.Where(c => EF.Functions.Like(c.Name, namePattern, LIKE_ESCAPE)
                        || c.Printings.Any(p => EF.Functions.Like(p.SetCode, codePattern, LIKE_ESCAPE)));
                }
            }

            var totalCount = await cards.CountAsync(cancellationToken).ConfigureAwait(false);
            var items = await cards
                .Include(c => c.Images)
                .Include(c => c.Printings)
                .AsSplitQuery()
                .OrderBy(c => c.Name)
                .ThenBy(c => c.ID)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return (items, totalCount);
        }
    }
}
