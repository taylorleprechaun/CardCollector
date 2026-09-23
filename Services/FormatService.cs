using CardCollector.Data.Models;
using CardCollector.Models;
using CardCollector.Repository;
using CardCollector.Rules;

namespace CardCollector.Services
{
    public sealed class FormatService : IFormatService
    {
        // Scoped, so this lives for one request: the page, EventService and AnalyticsService all read the formats,
        // and they are loaded once instead of once per caller. Any write through this service clears it.
        private IReadOnlyList<Format>? _formats;
        private readonly IFormatRepository _repository;

        public FormatService(IFormatRepository repository)
        {
            _repository = repository;
        }

        public async Task<SaveResult> AddAsync(Format format, CancellationToken cancellationToken = default)
        {
            if (format is null) throw new ArgumentNullException(nameof(format));

            var normalized = FormatRules.Normalize(format);
            normalized.ID = 0;

            var existing = await GetAllAsync(cancellationToken).ConfigureAwait(false);
            var errors = FormatRules.Validate(normalized, existing);
            if (errors.Count > 0)
                return SaveResult.Failure(errors);

            await _repository.AddAsync(normalized, cancellationToken).ConfigureAwait(false);
            _formats = null;
            return SaveResult.Success();
        }

        public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            var deleted = await _repository.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
            _formats = null;
            return deleted;
        }

        public async Task<IReadOnlyList<Format>> GetAllAsync(CancellationToken cancellationToken = default) =>
            _formats ??= await _repository.GetAllAsync(cancellationToken).ConfigureAwait(false);

        public async Task<SaveResult> UpdateAsync(Format format, CancellationToken cancellationToken = default)
        {
            if (format is null) throw new ArgumentNullException(nameof(format));

            var normalized = FormatRules.Normalize(format);

            var existing = await GetAllAsync(cancellationToken).ConfigureAwait(false);
            if (existing.All(f => f.ID != normalized.ID))
                return SaveResult.Missing("Format not found.");

            var errors = FormatRules.Validate(normalized, existing);
            if (errors.Count > 0)
                return SaveResult.Failure(errors);

            await _repository.UpdateAsync(normalized, cancellationToken).ConfigureAwait(false);
            _formats = null;
            return SaveResult.Success();
        }
    }
}
