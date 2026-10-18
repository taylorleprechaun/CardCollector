using CardCollector.Data;
using CardCollector.Data.Models.Catalog;
using CardCollector.Models.Catalog;
using Microsoft.EntityFrameworkCore;

namespace CardCollector.Repository.Catalog
{
    public sealed class CatalogRepository : ICatalogRepository
    {
        private readonly AppDBContext _context;

        public CatalogRepository(AppDBContext context)
        {
            _context = context;
        }

        public void AddCards(IEnumerable<CatalogCard> cards) => _context.CatalogCards.AddRange(cards);

        public void AddReviewItems(IEnumerable<CatalogReviewItem> items) => _context.CatalogReviewItems.AddRange(items);

        public void AddSets(IEnumerable<CatalogSet> sets) => _context.CatalogSets.AddRange(sets);

        public async Task AddSyncRunAsync(CatalogSyncRun run, CancellationToken cancellationToken = default)
        {
            if (run is null) throw new ArgumentNullException(nameof(run));

            _context.CatalogSyncRuns.Add(run);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        public void DiscardChanges() => _context.ChangeTracker.Clear();

        public async Task<IReadOnlyList<CatalogSyncRun>> GetRecentSyncRunsAsync(int count, CancellationToken cancellationToken = default) =>
            await _context.CatalogSyncRuns
                .AsNoTracking()
                .OrderByDescending(r => r.ID)
                .Take(count)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        public async Task<IReadOnlyList<CatalogReviewItem>> GetReviewItemsAsync(CancellationToken cancellationToken = default) =>
            await _context.CatalogReviewItems.ToListAsync(cancellationToken).ConfigureAwait(false);

        public async Task<CatalogState> LoadSnapshotAsync(CancellationToken cancellationToken = default)
        {
            // Each table is read flat and joined here: EF's Include fix-up over the whole catalog took about four times as long.
            var sets = await _context.CatalogSets.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
            var cards = await _context.CatalogCards.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
            var aliases = await _context.CatalogCardAliases.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
            var images = await _context.CatalogCardImages.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
            var printings = await _context.CatalogPrintings.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
            var sources = await _context.CatalogPrintingSources.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);

            var aliasesByCard = aliases.ToLookup(a => a.CatalogCardID);
            var cardsByID = cards.ToDictionary(c => c.ID);
            var imagesByCard = images.ToLookup(i => i.CatalogCardID);
            var printingsByCard = printings.ToLookup(p => p.CatalogCardID);
            var setsByID = sets.ToDictionary(s => s.ID);
            var sourcesByPrinting = sources.ToLookup(s => s.CatalogPrintingID);

            foreach (var card in cards)
            {
                card.Aliases = aliasesByCard[card.ID].ToList();
                card.Images = imagesByCard[card.ID].ToList();
                card.Printings = printingsByCard[card.ID].ToList();
            }

            foreach (var printing in printings)
            {
                printing.Card = cardsByID.GetValueOrDefault(printing.CatalogCardID);
                printing.Set = setsByID.GetValueOrDefault(printing.CatalogSetID);
                printing.Sources = sourcesByPrinting[printing.ID].ToList();
            }

            return new CatalogState { Cards = cards, Sets = sets };
        }

        public async Task<CatalogState> LoadStateAsync(CancellationToken cancellationToken = default)
        {
            var sets = await _context.CatalogSets.ToListAsync(cancellationToken).ConfigureAwait(false);
            var cards = await _context.CatalogCards
                .Include(c => c.Aliases)
                .Include(c => c.Images)
                .Include(c => c.Printings)
                    .ThenInclude(p => p.Sources)
                .AsSplitQuery()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            // Sets were loaded first in the same context, so every printing's Set navigation is already fixed up.
            return new CatalogState { Cards = cards, Sets = sets };
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        public async Task UpdateSyncRunAsync(CatalogSyncRun run, CancellationToken cancellationToken = default)
        {
            if (run is null) throw new ArgumentNullException(nameof(run));

            _context.CatalogSyncRuns.Update(run);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
