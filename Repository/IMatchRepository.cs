using CardCollector.Data.Models;

namespace CardCollector.Repository
{
    /// <summary>
    /// Provides data access for the rounds of an event.
    /// </summary>
    public interface IMatchRepository
    {
        /// <summary>
        /// Persists a new round at the zero-based position <paramref name="choosePosition"/> picks from the event's current
        /// rounds (clamped to the ends), moving the later ones down. Returns that position and the event's rounds in play
        /// order with the new one included, or null if the event does not exist.
        /// </summary>
        Task<(int Position, IReadOnlyList<Match> Rounds)?> AddAsync(
            int eventID,
            Match match,
            Func<IReadOnlyList<Match>, int> choosePosition,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes the round and renumbers the event's remaining rounds so they stay contiguous. Returns the remaining
        /// rounds in play order, or null if the event has no such round.
        /// </summary>
        Task<IReadOnlyList<Match>?> DeleteAsync(int eventID, int id, CancellationToken cancellationToken = default);

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
        /// Updates the round's own fields; its position in the event is left as it is. Returns the event's rounds in play
        /// order after the change, or null if the event has no such round.
        /// </summary>
        Task<IReadOnlyList<Match>?> UpdateAsync(int eventID, Match match, CancellationToken cancellationToken = default);
    }
}
