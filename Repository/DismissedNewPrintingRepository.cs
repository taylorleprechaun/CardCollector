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

        public async Task AddAsync(int cardID, string setCode, string rarityName)
        {
            var exists = await _context.DismissedNewPrintings
                .AnyAsync(d => d.CardID == cardID && d.SetCode == setCode && d.RarityName == rarityName)
                .ConfigureAwait(false);

            if (exists)
                return;

            _context.DismissedNewPrintings.Add(new DismissedNewPrinting
            {
                CardID = cardID,
                DateCreated = DateTime.UtcNow,
                DateModified = DateTime.UtcNow,
                RarityName = rarityName,
                SetCode = setCode
            });

            await _context.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task<bool> AnyAsync() =>
            await _context.DismissedNewPrintings.AnyAsync().ConfigureAwait(false);

        public async Task<IReadOnlySet<(int CardID, string SetCode, string RarityName)>> GetAllAsync()
        {
            var rows = await _context.DismissedNewPrintings
                .Select(d => new { d.CardID, d.SetCode, d.RarityName })
                .ToListAsync()
                .ConfigureAwait(false);

            return rows.Select(r => (r.CardID, r.SetCode, r.RarityName)).ToHashSet();
        }
    }
}
