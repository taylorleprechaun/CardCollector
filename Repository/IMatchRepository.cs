using CardCollector.Data.Models;

namespace CardCollector.Repository
{
    /// <summary>
    /// Provides data access for the rounds of an event.
    /// </summary>
    public interface IMatchRepository
    {
        /// <summary>
        /// Persists a new round and returns its ID. The round goes at the given zero-based position among the event's
        /// rounds, moving the later ones down, or at the end when no position is given.
        /// </summary>
        Task<int> AddAsync(int eventID, Match match, int? position = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes the round and renumbers the event's remaining rounds so they stay contiguous.
        /// Returns false if the event has no such round.
        /// </summary>
        Task<bool> DeleteAsync(int eventID, int id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the round, or null if the event has no such round.
        /// </summary>
        Task<Match?> GetAsync(int eventID, int id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the event's rounds in play order.
        /// </summary>
        Task<IReadOnlyList<Match>> GetByEventAsync(int eventID, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the distinct opponent decks from non-bye rounds, most frequently played first.
        /// </summary>
        Task<IReadOnlyList<string>> GetOpponentDecksAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Puts the event's rounds in the given order by renumbering them 1 to n, and returns how many changed position.
        /// Rounds not in the list are left as they are.
        /// </summary>
        Task<int> SetOrderAsync(int eventID, IReadOnlyList<int> orderedIDs, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates the round's own fields; its position in the event is left as it is.
        /// Returns false if the event has no such round.
        /// </summary>
        Task<bool> UpdateAsync(int eventID, Match match, CancellationToken cancellationToken = default);
    }
}
