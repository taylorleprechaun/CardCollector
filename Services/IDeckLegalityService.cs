using CardCollector.ViewModels;

namespace CardCollector.Services
{
    /// <summary>Evaluates a deck against the banlist in effect on an event's date, or the current one.</summary>
    public interface IDeckLegalityService
    {
        /// <summary>
        /// Builds the legality view for one tab. <paramref name="view"/> picks At event/Current (default: At event
        /// when the deck has events, else Current); <paramref name="eventID"/> picks which event At event resolves
        /// its list from (default: the most recent); <paramref name="listDate"/> overrides the resolved list for
        /// the active tab only. Never throws for missing or unreachable banlist data.
        /// </summary>
        Task<DeckLegalityViewModel> GetAsync(
            DeckDetailViewModel deck,
            DeckLegalityView? view,
            int? eventID,
            DateOnly? listDate,
            CancellationToken cancellationToken = default);
    }
}
