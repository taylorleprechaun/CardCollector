using CardCollector.Data.Models;
using CardCollector.Repository;
using CardCollector.ViewModels;

namespace CardCollector.Services
{
    public sealed class DeckLegalityService : IDeckLegalityService
    {
        private readonly IBanlistRepository _banlistRepository;

        public DeckLegalityService(IBanlistRepository banlistRepository)
        {
            _banlistRepository = banlistRepository;
        }

        public async Task<DeckLegalityViewModel> GetAsync(
            DeckDetailViewModel deck,
            DeckLegalityView? view,
            int? eventID,
            DateOnly? listDate,
            CancellationToken cancellationToken = default)
        {
            if (deck is null) throw new ArgumentNullException(nameof(deck));

            var current = await _banlistRepository.GetCurrentAsync().ConfigureAwait(false);
            var availableListDates = await _banlistRepository.GetAvailableListsAsync().ConfigureAwait(false);
            var isAvailable = current is not null || availableListDates.Count > 0;

            var atEventSource = ResolveSourceEvent(deck.Events, eventID);
            var activeView = view ?? (atEventSource is not null ? DeckLegalityView.AtEvent : DeckLegalityView.Current);

            var activeList = activeView == DeckLegalityView.Current
                ? await ResolveCurrentListAsync(current, listDate).ConfigureAwait(false)
                : await ResolveAtEventListAsync(atEventSource, listDate).ConfigureAwait(false);

            var cards = deck.Main.Cards.Concat(deck.Extra.Cards).Concat(deck.Side.Cards);
            var activeLegality = activeList is null ? null : DeckLegalityEvaluator.Evaluate(cards, activeList);

            return new DeckLegalityViewModel
            {
                ActiveLegality = activeLegality,
                ActiveView = activeView,
                AtEventSource = atEventSource,
                AvailableListDates = availableListDates,
                DeckID = deck.Deck.ID,
                Events = deck.Events,
                IsAvailable = isAvailable,
                RequestedListDate = listDate
            };
        }

        private Task<Banlist?> ResolveAtEventListAsync(Event? sourceEvent, DateOnly? listDate)
        {
            if (listDate is { } date)
                return _banlistRepository.GetListAsync(date);

            return sourceEvent is null
                ? Task.FromResult<Banlist?>(null)
                : _banlistRepository.GetListForDateAsync(sourceEvent.Date);
        }

        private Task<Banlist?> ResolveCurrentListAsync(Banlist? current, DateOnly? listDate) =>
            listDate is { } date ? _banlistRepository.GetListAsync(date) : Task.FromResult(current);

        private static Event? ResolveSourceEvent(IReadOnlyList<Event> events, int? eventID)
        {
            if (events.Count == 0)
                return null;

            return (eventID is { } id ? events.FirstOrDefault(e => e.ID == id) : null) ?? events[0];
        }
    }
}
