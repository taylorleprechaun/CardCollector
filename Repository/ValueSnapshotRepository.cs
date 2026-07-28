using CardCollector.Data;
using CardCollector.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace CardCollector.Repository
{
    public class ValueSnapshotRepository<TEntity> : IValueSnapshotRepository<TEntity>
        where TEntity : class, IValueSnapshotEntity
    {
        private readonly AppDBContext _context;
        private readonly string _tableName;

        public ValueSnapshotRepository(AppDBContext context, string tableName)
        {
            _context = context;
            _tableName = tableName;
        }

        public async Task<IEnumerable<TEntity>> GetAllSnapshotsAsync() =>
            await _context.Set<TEntity>()
                .OrderBy(s => s.SnapshotDate)
                .ToListAsync()
                .ConfigureAwait(false);

        public async Task<TEntity?> GetLatestSnapshotAsync() =>
            await _context.Set<TEntity>()
                .OrderByDescending(s => s.SnapshotDate)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);

        public async Task PruneSnapshotsAsync()
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-30).ToString("yyyy-MM-dd");

            // _tableName is a fixed literal supplied by our own subclass constructors (never external
            // input), so splicing it in via Replace is safe; the string itself is non-interpolated
            // (no leading $) so the {0} placeholders survive for ExecuteSqlRawAsync to bind cutoffDate to.
            var sql = """
                DELETE FROM "__TABLE__"
                WHERE SnapshotDate < {0}
                  AND SnapshotDate NOT IN (
                      SELECT MAX(SnapshotDate)
                      FROM "__TABLE__"
                      WHERE SnapshotDate < {0}
                      GROUP BY substr(SnapshotDate, 1, 7)
                  )
                """.Replace("__TABLE__", _tableName);

            await _context.Database.ExecuteSqlRawAsync(sql, cutoffDate).ConfigureAwait(false);
        }

        public async Task UpsertSnapshotAsync(TEntity snapshot)
        {
            var existing = await _context.Set<TEntity>()
                .FirstOrDefaultAsync(s => s.SnapshotDate == snapshot.SnapshotDate)
                .ConfigureAwait(false);

            if (existing is not null)
            {
                existing.Count = snapshot.Count;
                existing.TotalValue = snapshot.TotalValue;
            }
            else
            {
                _context.Set<TEntity>().Add(snapshot);
            }

            await _context.SaveChangesAsync().ConfigureAwait(false);
        }
    }
}
