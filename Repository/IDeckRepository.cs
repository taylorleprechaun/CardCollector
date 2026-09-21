using CardCollector.Data.Models;
using CardCollector.ViewModels;

namespace CardCollector.Repository
{
    /// <summary>
    /// Provides data access for decks and their cards.
    /// </summary>
    public interface IDeckRepository
    {
        /// <summary>
        /// Persists a new deck with its cards and returns its ID.
        /// </summary>
        Task<int> AddAsync(Deck deck, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes the deck and its cards, and clears the deck link on every event that used it, all in one transaction.
        /// Returns false if the deck does not exist.
        /// </summary>
        Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns every deck with its card counts and the number of events that used it, sorted by name.
        /// </summary>
        Task<IReadOnlyList<DeckListItemViewModel>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the deck, optionally with its cards in section order, or null if not found.
        /// </summary>
        Task<Deck?> GetAsync(int id, bool includeCards, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates the deck's name and notes. Its cards are left as they are. Returns false if the deck does not exist.
        /// </summary>
        Task<bool> UpdateAsync(Deck deck, CancellationToken cancellationToken = default);
    }
}
