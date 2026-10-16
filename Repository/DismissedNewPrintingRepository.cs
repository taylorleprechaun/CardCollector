using CardCollector.Data;
using CardCollector.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace CardCollector.Repository
{
    public sealed class DismissedNewPrintingRepository : IDismissedNewPrintingRepository
    {
        private readonly AppDBContext _context;

        public DismissedNewPrintingRepository(AppDBContext context)
        {
            _context = context;
        }

        public async Task AddAsync(IReadOnlyCollection<int> printingIDs)
        {
            if (printingIDs is null) throw new ArgumentNullException(nameof(printingIDs));

            var requested = printingIDs.ToHashSet();
            if (requested.Count == 0)
                return;

            var existing = await _context.DismissedNewPrintings
                .Where(d => requested.Contains(d.CatalogPrintingID))
                .Select(d => d.CatalogPrintingID)
                .ToListAsync()
                .ConfigureAwait(false);
            requested.ExceptWith(existing);
            if (requested.Count == 0)
                return;

            var now = DateTime.UtcNow;
            _context.DismissedNewPrintings.AddRange(requested.Select(printingID => new DismissedNewPrinting
            {
                CatalogPrintingID = printingID,
                DateCreated = now,
                DateModified = now
            }));

            await _context.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task<bool> AnyAsync() =>
            await _context.DismissedNewPrintings.AnyAsync().ConfigureAwait(false);

        public async Task<IReadOnlySet<int>> GetAllAsync()
        {
            var ids = await _context.DismissedNewPrintings
                .Select(d => d.CatalogPrintingID)
                .ToListAsync()
                .ConfigureAwait(false);

            return ids.ToHashSet();
        }
    }
}
