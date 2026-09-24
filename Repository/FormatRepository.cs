using CardCollector.Data;
using CardCollector.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace CardCollector.Repository
{
    public sealed class FormatRepository : IFormatRepository
    {
        private readonly AppDBContext _context;

        public FormatRepository(AppDBContext context)
        {
            _context = context;
        }

        public async Task<int> AddAsync(Format format, CancellationToken cancellationToken = default)
        {
            if (format is null) throw new ArgumentNullException(nameof(format));

            var now = DateTime.UtcNow;
            var entity = new Format
            {
                DateCreated = now,
                DateModified = now,
                EndDate = format.EndDate,
                Name = format.Name,
                Notes = format.Notes,
                StartDate = format.StartDate,
                Strategies = CopyStrategies(format.Strategies)
            };

            _context.Formats.Add(entity);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return entity.ID;
        }

        public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            var entity = await _context.Formats
                .Include(f => f.Strategies)
                .FirstOrDefaultAsync(f => f.ID == id, cancellationToken)
                .ConfigureAwait(false);

            if (entity is null)
                return false;

            _context.Formats.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }

        public async Task<IReadOnlyList<Format>> GetAllAsync(CancellationToken cancellationToken = default) =>
            await _context.Formats
                .AsNoTracking()
                .Include(f => f.Strategies.OrderBy(s => s.Position))
                .OrderByDescending(f => f.StartDate)
                .ThenByDescending(f => f.ID)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        public async Task<bool> UpdateAsync(Format format, CancellationToken cancellationToken = default)
        {
            if (format is null) throw new ArgumentNullException(nameof(format));

            var entity = await _context.Formats
                .Include(f => f.Strategies)
                .FirstOrDefaultAsync(f => f.ID == format.ID, cancellationToken)
                .ConfigureAwait(false);

            if (entity is null)
                return false;

            _context.FormatStrategies.RemoveRange(entity.Strategies.ToList());
            entity.Strategies = CopyStrategies(format.Strategies);

            entity.DateModified = DateTime.UtcNow;
            entity.EndDate = format.EndDate;
            entity.Name = format.Name;
            entity.Notes = format.Notes;
            entity.StartDate = format.StartDate;

            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }

        private static IReadOnlyList<FormatStrategy> CopyStrategies(IEnumerable<FormatStrategy> strategies) =>
            strategies
                .Select((s, index) => new FormatStrategy { Name = s.Name, Position = index })
                .ToList();
    }
}
