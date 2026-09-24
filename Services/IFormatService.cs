using CardCollector.Data.Models;
using CardCollector.Models;

namespace CardCollector.Services
{
    /// <summary>
    /// Manages formats: validates input, then persists it.
    /// </summary>
    public interface IFormatService
    {
        /// <summary>
        /// Validates and adds a new format. Validation failures are returned in the result rather than thrown.
        /// </summary>
        Task<SaveResult> AddAsync(Format format, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes the format. Returns false if it does not exist.
        /// </summary>
        Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns all formats, newest start date first, each with its strategies in ranked order.
        /// </summary>
        Task<IReadOnlyList<Format>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates and updates an existing format. Validation failures are returned in the result rather than thrown.
        /// </summary>
        Task<SaveResult> UpdateAsync(Format format, CancellationToken cancellationToken = default);
    }
}
