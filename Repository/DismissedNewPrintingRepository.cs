using CardCollector.Data;
using CardCollector.Data.Models;
using CardCollector.DTO;
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

        public async Task AddAsync(int printingID)
        {
            var exists = await _context.DismissedNewPrintings
                .AnyAsync(d => d.CatalogPrintingID == printingID)
                .ConfigureAwait(false);

            if (exists)
                return;

            _context.DismissedNewPrintings.Add(new DismissedNewPrinting
            {
                CatalogPrintingID = printingID,
                DateCreated = DateTime.UtcNow,
                DateModified = DateTime.UtcNow
            });

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
