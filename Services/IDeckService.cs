using CardCollector.Models;
using CardCollector.ViewModels;

namespace CardCollector.Services
{
    /// <summary>
    /// Manages decks: imports pasted deck lists, links decks to events, and prepares decks for viewing.
    /// </summary>
    public interface IDeckService
    {
        /// <summary>
        /// Deletes the deck and its cards; events that used it are left with no deck. Returns false if it does not exist.
        /// </summary>
        Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns every deck with its card counts and the number of events that used it, sorted by name.
        /// </summary>
        Task<IReadOnlyList<DeckListItemViewModel>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the deck with its cards resolved and the events that used it, or null if it does not exist.
        /// </summary>
        Task<DeckDetailViewModel?> GetAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Parses a pasted deck list and stores it as a new deck, linking it to the requested event. A list that can't
        /// be parsed is reported in the result rather than thrown, and nothing is stored.
        /// </summary>
        Task<DeckImportResult> ImportAsync(DeckImportRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Points an existing event, and optionally the other events with the same decklist URL and no deck, at an existing deck.
        /// Returns how many events were linked; 0 when the event or the deck does not exist.
        /// </summary>
        Task<int> LinkEventAsync(int eventID, int deckID, bool linkOtherEventsWithSameUrl = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Parses a pasted deck list without storing anything, so its size and any cards that can't be matched can be shown first.
        /// </summary>
        DeckImportResult Preview(string? text);

        /// <summary>
        /// Clears the event's deck. Returns false if the event does not exist.
        /// </summary>
        Task<bool> UnlinkEventAsync(int eventID, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates and updates a deck's name and notes. Validation failures are returned in the result rather than thrown.
        /// </summary>
        Task<DeckSaveResult> UpdateAsync(int id, string? name, string? notes, CancellationToken cancellationToken = default);
    }
}
