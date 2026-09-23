using CardCollector.Data.Models;
using CardCollector.Models;
using CardCollector.Repository;
using CardCollector.Rules;
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

            var current = await _banlistRepository.GetCurrentAsync(cancellationToken).ConfigureAwait(false);
            var availableListDates = await _banlistRepository.GetAvailableListsAsync(cancellationToken).ConfigureAwait(false);
            var isAvailable = current is not null || availableListDates.Count > 0;

            var atEventSource = ResolveSourceEvent(deck.Events, eventID);
            var activeView = view ?? (atEventSource is not null ? DeckLegalityView.AtEvent : DeckLegalityView.Current);

            var activeList = activeView == DeckLegalityView.Current
                ? await ResolveCurrentListAsync(current, listDate, cancellationToken).ConfigureAwait(false)
                : await ResolveAtEventListAsync(atEventSource, listDate, cancellationToken).ConfigureAwait(false);

            var cards = deck.Main.Cards.Concat(deck.Extra.Cards).Concat(deck.Side.Cards);

            return new DeckLegalityViewModel
            {
                ActiveView = activeView,
                AtEventSource = atEventSource,
                AvailableListDates = availableListDates,
                CardStatuses = activeList is null ? null : DeckLegalityEvaluator.Evaluate(cards, activeList),
                DeckID = deck.Deck.ID,
                Events = deck.Events,
                IsAvailable = isAvailable,
                RequestedListDate = listDate
            };
        }

        private Task<Banlist?> ResolveAtEventListAsync(Event? sourceEvent, DateOnly? listDate, CancellationToken cancellationToken)
        {
            if (listDate is { } date)
                return _banlistRepository.GetListAsync(date, cancellationToken);

            return sourceEvent is null
                ? Task.FromResult<Banlist?>(null)
                : _banlistRepository.GetListForDateAsync(sourceEvent.Date, cancellationToken);
        }

        private Task<Banlist?> ResolveCurrentListAsync(Banlist? current, DateOnly? listDate, CancellationToken cancellationToken) =>
            listDate is { } date ? _banlistRepository.GetListAsync(date, cancellationToken) : Task.FromResult(current);

        private static Event? ResolveSourceEvent(IReadOnlyList<Event> events, int? eventID)
        {
            if (events.Count == 0)
                return null;

            return (eventID is { } id ? events.FirstOrDefault(e => e.ID == id) : null) ?? events[0];
        }
    }
}
