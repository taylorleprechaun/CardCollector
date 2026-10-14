using System.Globalization;
using CardCollector.Data;
using CardCollector.Data.Models.Catalog;
using CardCollector.Models.Catalog;
using Microsoft.EntityFrameworkCore;

namespace CardCollector.Repository.Catalog
{
    /// <remarks>
    /// User rows written since the migration don't carry <c>CatalogPrintingID</c> yet, so every lookup of a printing's
    /// rows also matches the printing's (card, set code, rarity, variant) columns.
    /// </remarks>
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
            var printings = await _context.CatalogPrintings
                .AsNoTracking()
                .Where(p => p.CatalogCardID == catalogCardID)
                .Select(p => new { p.ID, p.SetCode, p.RarityName, p.PrintVariant })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            if (printings.Count == 0)
                return new Dictionary<int, int>();

            var ids = printings.Select(p => p.ID).ToList();
            var rows = new List<(int? CatalogPrintingID, string SetCode, string? RarityName, string? PrintVariant)>();
            rows.AddRange((await _context.CollectionEntries.AsNoTracking()
                .Where(e => (e.CatalogPrintingID != null && ids.Contains(e.CatalogPrintingID.Value)) || e.CardID == catalogCardID)
                .Select(e => new { e.CatalogPrintingID, e.SetCode, e.RarityName, e.PrintVariant })
                .ToListAsync(cancellationToken).ConfigureAwait(false))
                .Select(e => (e.CatalogPrintingID, e.SetCode, e.RarityName, e.PrintVariant)));
            rows.AddRange((await _context.PreferredVersions.AsNoTracking()
                .Where(v => (v.CatalogPrintingID != null && ids.Contains(v.CatalogPrintingID.Value)) || v.CardID == catalogCardID)
                .Select(v => new { v.CatalogPrintingID, v.SetCode, v.RarityName, v.PrintVariant })
                .ToListAsync(cancellationToken).ConfigureAwait(false))
                .Select(v => (v.CatalogPrintingID, v.SetCode, v.RarityName, v.PrintVariant)));
            rows.AddRange((await _context.CheckedOutCards.AsNoTracking()
                .Where(c => (c.CatalogPrintingID != null && ids.Contains(c.CatalogPrintingID.Value)) || c.CardID == catalogCardID)
                .Select(c => new { c.CatalogPrintingID, c.SetCode, c.RarityName, c.PrintVariant })
                .ToListAsync(cancellationToken).ConfigureAwait(false))
                .Select(c => (c.CatalogPrintingID, c.SetCode, (string?)c.RarityName, c.PrintVariant)));
            rows.AddRange((await _context.PendingOrderLines.AsNoTracking()
                .Where(l => (l.CatalogPrintingID != null && ids.Contains(l.CatalogPrintingID.Value)) || l.CardID == catalogCardID)
                .Select(l => new { l.CatalogPrintingID, l.SetCode, l.RarityName, l.PrintVariant })
                .ToListAsync(cancellationToken).ConfigureAwait(false))
                .Select(l => (l.CatalogPrintingID, l.SetCode, l.RarityName, l.PrintVariant)));

            var counts = new Dictionary<int, int>();
            foreach (var row in rows)
            {
                var printingID = row.CatalogPrintingID is int id && ids.Contains(id)
                    ? id
                    : printings.FirstOrDefault(p => p.SetCode == row.SetCode && p.RarityName == row.RarityName && p.PrintVariant == row.PrintVariant)?.ID;
                if (printingID is int matched)
                    counts[matched] = counts.GetValueOrDefault(matched) + 1;
            }

            return counts;
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

            var (fromID, fromCard, fromCode, fromRarity, fromVariant) = (from.ID, from.CatalogCardID, from.SetCode, from.RarityName, from.PrintVariant);
            var (intoID, intoCard, intoCode, intoRarity, intoVariant) = (into.ID, into.CatalogCardID, into.SetCode, into.RarityName, into.PrintVariant);
            var moved = 0;
            var folded = 0;

            foreach (var entry in await _context.CollectionEntries
                .Where(e => e.CatalogPrintingID == fromID || (e.CardID == fromCard && e.SetCode == fromCode && e.RarityName == fromRarity && e.PrintVariant == fromVariant))
                .ToListAsync(cancellationToken).ConfigureAwait(false))
            {
                (entry.CatalogPrintingID, entry.CardID, entry.SetCode, entry.RarityName, entry.PrintVariant) = (intoID, intoCard, intoCode, intoRarity, intoVariant);
                moved++;
            }

            foreach (var line in await _context.PendingOrderLines
                .Where(l => l.CatalogPrintingID == fromID || (l.CardID == fromCard && l.SetCode == fromCode && l.RarityName == fromRarity && l.PrintVariant == fromVariant))
                .ToListAsync(cancellationToken).ConfigureAwait(false))
            {
                (line.CatalogPrintingID, line.CardID, line.SetCode, line.RarityName, line.PrintVariant) = (intoID, intoCard, intoCode, intoRarity, intoVariant);
                moved++;
            }

            var keptVersion = await _context.PreferredVersions
                .FirstOrDefaultAsync(v => v.CatalogPrintingID == intoID || (v.CardID == intoCard && v.SetCode == intoCode && v.RarityName == intoRarity && v.PrintVariant == intoVariant), cancellationToken)
                .ConfigureAwait(false);
            foreach (var version in await _context.PreferredVersions
                .Where(v => v.CatalogPrintingID == fromID || (v.CardID == fromCard && v.SetCode == fromCode && v.RarityName == fromRarity && v.PrintVariant == fromVariant))
                .ToListAsync(cancellationToken).ConfigureAwait(false))
            {
                if (keptVersion is not null)
                {
                    keptVersion.DesiredQuantity = Math.Max(keptVersion.DesiredQuantity, version.DesiredQuantity);
                    _context.PreferredVersions.Remove(version);
                    folded++;
                    continue;
                }

                (version.CatalogPrintingID, version.CardID, version.SetCode, version.RarityName, version.PrintVariant) = (intoID, intoCard, intoCode, intoRarity, intoVariant);
                keptVersion = version;
                moved++;
            }

            var keptCheckout = await _context.CheckedOutCards
                .FirstOrDefaultAsync(c => c.CatalogPrintingID == intoID || (c.CardID == intoCard && c.SetCode == intoCode && c.RarityName == intoRarity && c.PrintVariant == intoVariant), cancellationToken)
                .ConfigureAwait(false);
            foreach (var checkout in await _context.CheckedOutCards
                .Where(c => c.CatalogPrintingID == fromID || (c.CardID == fromCard && c.SetCode == fromCode && c.RarityName == fromRarity && c.PrintVariant == fromVariant))
                .ToListAsync(cancellationToken).ConfigureAwait(false))
            {
                if (keptCheckout is not null)
                {
                    keptCheckout.Quantity += checkout.Quantity;
                    _context.CheckedOutCards.Remove(checkout);
                    folded++;
                    continue;
                }

                (checkout.CatalogPrintingID, checkout.CardID, checkout.SetCode, checkout.RarityName, checkout.PrintVariant) = (intoID, intoCard, intoCode, intoRarity, intoVariant);
                keptCheckout = checkout;
                moved++;
            }

            var keptDismissal = await _context.DismissedNewPrintings
                .FirstOrDefaultAsync(d => d.CatalogPrintingID == intoID || (d.CardID == intoCard && d.SetCode == intoCode && d.RarityName == intoRarity && d.PrintVariant == intoVariant), cancellationToken)
                .ConfigureAwait(false);
            foreach (var dismissal in await _context.DismissedNewPrintings
                .Where(d => d.CatalogPrintingID == fromID || (d.CardID == fromCard && d.SetCode == fromCode && d.RarityName == fromRarity && d.PrintVariant == fromVariant))
                .ToListAsync(cancellationToken).ConfigureAwait(false))
            {
                if (keptDismissal is not null)
                {
                    _context.DismissedNewPrintings.Remove(dismissal);
                    folded++;
                    continue;
                }

                (dismissal.CatalogPrintingID, dismissal.CardID, dismissal.SetCode, dismissal.RarityName, dismissal.PrintVariant) = (intoID, intoCard, intoCode, intoRarity, intoVariant);
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
