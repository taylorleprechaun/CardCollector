using CardCollector.Data.Models;
using CardCollector.ViewModels;

namespace CardCollector.Services
{
    /// <summary>
    /// Manages events: validates input, persists it, and derives each event's format and results.
    /// </summary>
    public interface IEventService
    {
        /// <summary>
        /// Validates and adds a new event. Validation failures are returned in the result rather than thrown.
        /// </summary>
        Task<EventSaveResult> AddAsync(Event tournamentEvent, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes the event and its rounds. Returns false if it does not exist.
        /// </summary>
        Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the event with its rounds, its format and its tallies, or null if it does not exist.
        /// </summary>
        Task<EventDetailViewModel?> GetAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the distinct deck names used by events, sorted alphabetically.
        /// </summary>
        Task<IReadOnlyList<string>> GetDeckNamesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the distinct locations used by events, sorted alphabetically.
        /// </summary>
        Task<IReadOnlyList<string>> GetLocationsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns one page of events matching the criteria, newest first. Filtering by format matches the events
        /// whose date falls in that format's date range.
        /// </summary>
        Task<PagedResult<EventListItemViewModel>> SearchAsync(EventSearchCriteria criteria, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates and updates an existing event. Validation failures are returned in the result rather than thrown.
        /// </summary>
        Task<EventSaveResult> UpdateAsync(Event tournamentEvent, CancellationToken cancellationToken = default);
    }
}
