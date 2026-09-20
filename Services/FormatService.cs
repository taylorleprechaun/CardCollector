using CardCollector.Data.Models;
using CardCollector.Repository;

namespace CardCollector.Services
{
    public sealed class FormatService : IFormatService
    {
        private readonly IFormatRepository _repository;

        public FormatService(IFormatRepository repository)
        {
            _repository = repository;
        }

        public async Task<FormatSaveResult> AddAsync(Format format, CancellationToken cancellationToken = default)
        {
            if (format is null) throw new ArgumentNullException(nameof(format));

            var normalized = FormatRules.Normalize(format);
            normalized.ID = 0;

            var existing = await _repository.GetAllAsync(cancellationToken).ConfigureAwait(false);
            var errors = FormatRules.Validate(normalized, existing);
            if (errors.Count > 0)
                return FormatSaveResult.Failure(errors);

            await _repository.AddAsync(normalized, cancellationToken).ConfigureAwait(false);
            return FormatSaveResult.Success();
        }

        public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default) =>
            _repository.DeleteAsync(id, cancellationToken);

        public Task<IReadOnlyList<Format>> GetAllAsync(CancellationToken cancellationToken = default) =>
            _repository.GetAllAsync(cancellationToken);

        public async Task<FormatSaveResult> UpdateAsync(Format format, CancellationToken cancellationToken = default)
        {
            if (format is null) throw new ArgumentNullException(nameof(format));

            var normalized = FormatRules.Normalize(format);

            var existing = await _repository.GetAllAsync(cancellationToken).ConfigureAwait(false);
            if (existing.All(f => f.ID != normalized.ID))
                return FormatSaveResult.Failure(["Format not found."]);

            var errors = FormatRules.Validate(normalized, existing);
            if (errors.Count > 0)
                return FormatSaveResult.Failure(errors);

            await _repository.UpdateAsync(normalized, cancellationToken).ConfigureAwait(false);
            return FormatSaveResult.Success();
        }
    }
}
