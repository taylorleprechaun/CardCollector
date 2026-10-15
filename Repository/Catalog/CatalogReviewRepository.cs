using CardCollector.Data;
using CardCollector.Data.Models.Catalog;
using Microsoft.EntityFrameworkCore;

namespace CardCollector.Repository.Catalog
{
    public sealed class CatalogReviewRepository : ICatalogReviewRepository
    {
        private readonly AppDBContext _context;

        public CatalogReviewRepository(AppDBContext context)
        {
            _context = context;
        }

        public void AddCard(CatalogCard card) => _context.CatalogCards.Add(card);

        public void AddSet(CatalogSet set) => _context.CatalogSets.Add(set);

        public async Task<int> CountOpenAsync(IReadOnlyCollection<CatalogReviewItemKind> kinds, CancellationToken cancellationToken = default) =>
            await _context.CatalogReviewItems
                .CountAsync(i => i.Status == CatalogReviewItemStatus.Open && kinds.Contains(i.Kind), cancellationToken)
                .ConfigureAwait(false);

        public async Task<IReadOnlyDictionary<CatalogReviewItemKind, int>> CountOpenByKindAsync(CancellationToken cancellationToken = default) =>
            await _context.CatalogReviewItems
                .Where(i => i.Status == CatalogReviewItemStatus.Open)
                .GroupBy(i => i.Kind)
                .Select(g => new { Kind = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Kind, x => x.Count, cancellationToken)
                .ConfigureAwait(false);

        public async Task<int> CountUserRowsForCardAsync(int catalogCardID, CancellationToken cancellationToken = default) =>
            await _context.CollectionEntries.CountAsync(e => e.CatalogPrinting!.CatalogCardID == catalogCardID, cancellationToken).ConfigureAwait(false)
            + await _context.PreferredVersions.CountAsync(v => v.CatalogPrinting!.CatalogCardID == catalogCardID, cancellationToken).ConfigureAwait(false)
            + await _context.CheckedOutCards.CountAsync(c => c.CatalogPrinting!.CatalogCardID == catalogCardID, cancellationToken).ConfigureAwait(false)
            + await _context.PendingOrderLines.CountAsync(l => l.CatalogPrinting!.CatalogCardID == catalogCardID, cancellationToken).ConfigureAwait(false);

        public async Task<int> CountUserRowsForPrintingAsync(int catalogPrintingID, CancellationToken cancellationToken = default) =>
            await _context.CollectionEntries.CountAsync(e => e.CatalogPrintingID == catalogPrintingID, cancellationToken).ConfigureAwait(false)
            + await _context.PreferredVersions.CountAsync(v => v.CatalogPrintingID == catalogPrintingID, cancellationToken).ConfigureAwait(false)
            + await _context.CheckedOutCards.CountAsync(c => c.CatalogPrintingID == catalogPrintingID, cancellationToken).ConfigureAwait(false)
            + await _context.PendingOrderLines.CountAsync(l => l.CatalogPrintingID == catalogPrintingID, cancellationToken).ConfigureAwait(false);

        public async Task<CatalogCard?> FindCardByNameAsync(string name, CancellationToken cancellationToken = default) =>
            await _context.CatalogCards
                .AsNoTracking()
                .OrderBy(c => c.Status)
                .FirstOrDefaultAsync(c => c.Name.ToLower() == name.ToLower(), cancellationToken)
                .ConfigureAwait(false);

        public async Task<CatalogCard?> GetCardAsync(int catalogCardID, CancellationToken cancellationToken = default) =>
            await _context.CatalogCards
                .Include(c => c.Printings)
                    .ThenInclude(p => p.Sources)
                .Include(c => c.Printings)
                    .ThenInclude(p => p.Set)
                .AsSplitQuery()
                .FirstOrDefaultAsync(c => c.ID == catalogCardID, cancellationToken)
                .ConfigureAwait(false);

        public async Task<CatalogReviewItem?> GetItemAsync(int id, CancellationToken cancellationToken = default) =>
            await _context.CatalogReviewItems.FirstOrDefaultAsync(i => i.ID == id, cancellationToken).ConfigureAwait(false);

        public async Task<IReadOnlyList<CatalogReviewItem>> GetOpenItemsAsync(
            IReadOnlyCollection<CatalogReviewItemKind> kinds,
            CancellationToken cancellationToken = default) =>
            await _context.CatalogReviewItems
                .Where(i => i.Status == CatalogReviewItemStatus.Open && kinds.Contains(i.Kind))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        public async Task<CatalogPrinting?> GetPrintingAsync(int catalogPrintingID, CancellationToken cancellationToken = default) =>
            await _context.CatalogPrintings
                .Include(p => p.Card)
                .Include(p => p.Sources)
                .Include(p => p.Set)
                .FirstOrDefaultAsync(p => p.ID == catalogPrintingID, cancellationToken)
                .ConfigureAwait(false);

        public async Task<CatalogSet?> GetSetByPrefixAsync(string prefix, CancellationToken cancellationToken = default) =>
            await _context.CatalogSets.FirstOrDefaultAsync(s => s.Prefix == prefix, cancellationToken).ConfigureAwait(false);

        public async Task<bool> IsSourceLinkedAsync(CatalogSource source, string externalKey, CancellationToken cancellationToken = default) =>
            await _context.CatalogPrintingSources
                .AnyAsync(s => s.Source == source && s.ExternalKey == externalKey, cancellationToken)
                .ConfigureAwait(false);

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        public async Task<(IReadOnlyList<CatalogReviewItem> Items, int TotalCount)> SearchAsync(
            IReadOnlyCollection<CatalogReviewItemStatus> statuses,
            IReadOnlyCollection<CatalogReviewItemKind> kinds,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            var query = _context.CatalogReviewItems
                .AsNoTracking()
                .Where(i => statuses.Contains(i.Status) && kinds.Contains(i.Kind));

            var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);
            var items = await query
                .OrderByDescending(i => i.LastDetectedUTC)
                .ThenByDescending(i => i.ID)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return (items, totalCount);
        }
    }
}
