using CardCollector.Data.Models;

namespace CardCollector.Repository
{
    /// <summary>
    /// Provides data access for formats and their ranked strategies.
    /// </summary>
    public interface IFormatRepository
    {
        /// <summary>
        /// Persists a new format with its strategies and returns the new format's ID.
        /// </summary>
        Task<int> AddAsync(Format format, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes the format and its strategies. Returns false if the format does not exist.
        /// </summary>
        Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns all formats, newest start date first, each with its strategies in ranked order.
        /// </summary>
        Task<IReadOnlyList<Format>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the format with its strategies in ranked order, or null if not found.
        /// </summary>
        Task<Format?> GetAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates the format's fields and replaces its strategies with the supplied list. Returns false if the format does not exist.
        /// </summary>
        Task<bool> UpdateAsync(Format format, CancellationToken cancellationToken = default);
    }
}
