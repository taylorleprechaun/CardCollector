using CardCollector.Data.Models;
using CardCollector.ViewModels;

namespace CardCollector.Repository
{
    /// <summary>
    /// Provides data access for events and their rounds.
    /// </summary>
    public interface IEventRepository
    {
        /// <summary>
        /// Persists a new event and returns its ID. Any rounds on the supplied event are ignored.
        /// </summary>
        Task<int> AddAsync(Event tournamentEvent, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes the event and its rounds. Returns false if the event does not exist.
        /// </summary>
        Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the event, optionally with its rounds in play order, or null if not found.
        /// </summary>
        Task<Event?> GetAsync(int id, bool includeMatches, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the distinct deck names used by events, sorted alphabetically.
        /// </summary>
        Task<IReadOnlyList<string>> GetDeckNamesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the distinct locations used by events, sorted alphabetically.
        /// </summary>
        Task<IReadOnlyList<string>> GetLocationsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns one page of events matching the criteria, newest first, each with its rounds.
        /// </summary>
        Task<PagedResult<Event>> SearchAsync(EventSearchCriteria criteria, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates the event's own fields. Its rounds and deck link are left as they are. Returns false if the event does not exist.
        /// </summary>
        Task<bool> UpdateAsync(Event tournamentEvent, CancellationToken cancellationToken = default);
    }
}
