using CardCollector.Data.Models;
using CardCollector.ViewModels;

namespace CardCollector.Services
{
    /// <summary>
    /// Manages the rounds of an event: validates input, persists it, and summarizes the results.
    /// </summary>
    public interface IMatchService
    {
        /// <summary>
        /// Validates and adds a round, placing it among the event's rounds by its round label. Validation failures are
        /// returned in the result rather than thrown.
        /// </summary>
        Task<MatchSaveResult> AddAsync(int eventID, Match match, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a round and renumbers the rest. Returns false if the event has no such round.
        /// </summary>
        Task<bool> DeleteAsync(int eventID, int id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the distinct opponent decks from earlier rounds, most frequently played first.
        /// </summary>
        Task<IReadOnlyList<string>> GetOpponentDecksAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the tallies for the event's rounds and the label suggested for the next round.
        /// </summary>
        Task<MatchSummaryViewModel> GetSummaryAsync(int eventID, CancellationToken cancellationToken = default);

        /// <summary>
        /// Puts the event's rounds in round order: numbered rounds, then Top 8, Top 4 and Finals. Returns false when
        /// they were already in order.
        /// </summary>
        Task<bool> SortAsync(int eventID, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates and updates an existing round. Validation failures are returned in the result rather than thrown.
        /// </summary>
        Task<MatchSaveResult> UpdateAsync(int eventID, Match match, CancellationToken cancellationToken = default);
    }
}
