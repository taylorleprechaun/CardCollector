using CardCollector.Data.Models;
using CardCollector.Models;
using CardCollector.Repository;
using CardCollector.Rules;
using CardCollector.ViewModels;

namespace CardCollector.Services
{
    public sealed class DeckService : IDeckService
    {
        private readonly ICardDataRepository _cardDataRepository;
        private readonly IDeckRepository _deckRepository;
        private readonly IEventRepository _eventRepository;
        private readonly IUnitOfWork _unitOfWork;

        public DeckService(ICardDataRepository cardDataRepository, IDeckRepository deckRepository, IEventRepository eventRepository, IUnitOfWork unitOfWork)
        {
            _cardDataRepository = cardDataRepository;
            _deckRepository = deckRepository;
            _eventRepository = eventRepository;
            _unitOfWork = unitOfWork;
        }

        public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default) =>
            _deckRepository.DeleteAsync(id, cancellationToken);

        public Task<IReadOnlyList<DeckListItemViewModel>> GetAllAsync(CancellationToken cancellationToken = default) =>
            _deckRepository.GetAllAsync(cancellationToken);

        public async Task<DeckDetailViewModel?> GetAsync(int id, CancellationToken cancellationToken = default)
        {
            var deck = await _deckRepository.GetAsync(id, includeCards: true, cancellationToken).ConfigureAwait(false);
            if (deck is null)
                return null;

            var events = await _eventRepository.GetByDeckAsync(id, cancellationToken).ConfigureAwait(false);
            var main = BuildSection(deck, DeckSection.Main);

            return new DeckDetailViewModel
            {
                Deck = deck,
                Events = events,
                Extra = BuildSection(deck, DeckSection.Extra),
                Main = main,
                MainTypes = DeckRules.CountTypes(main.Cards),
                Side = BuildSection(deck, DeckSection.Side)
            };
        }

        public async Task<DeckImportResult> ImportAsync(DeckImportRequest request, CancellationToken cancellationToken = default)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));

            var (cards, error) = ParseAndBuild(request.Text);
            if (cards is null)
                return DeckImportResult.Failure(error!);

            Event? tournamentEvent = null;
            if (request.EventID is { } eventID)
            {
                tournamentEvent = await _eventRepository.GetAsync(eventID, includeMatches: false, cancellationToken).ConfigureAwait(false);
                if (tournamentEvent is null)
                    return DeckImportResult.Failure("Event not found.");
            }

            var deck = DeckRules.Normalize(new Deck { Name = request.Name ?? string.Empty });
            if (string.IsNullOrEmpty(deck.Name))
                deck.Name = tournamentEvent?.DeckName ?? string.Empty;

            var errors = DeckRules.Validate(deck);
            if (errors.Count > 0)
                return DeckImportResult.Failure(errors);

            deck.Cards = cards;

            var deckID = 0;
            var linkedEventCount = 0;

            // The deck and the events that point at it must succeed or fail together.
            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                deckID = await _deckRepository.AddAsync(deck, cancellationToken).ConfigureAwait(false);
                if (tournamentEvent is null)
                    return;

                var eventIDs = await GetEventIDsToLinkAsync(tournamentEvent, request.LinkOtherEventsWithSameUrl, cancellationToken).ConfigureAwait(false);
                linkedEventCount = await _eventRepository.SetDeckAsync(eventIDs, deckID, cancellationToken).ConfigureAwait(false);
            }).ConfigureAwait(false);

            return Summarize(cards, deckID, linkedEventCount);
        }

        public async Task<int> LinkEventAsync(int eventID, int deckID, bool linkOtherEventsWithSameUrl = false, CancellationToken cancellationToken = default)
        {
            var deck = await _deckRepository.GetAsync(deckID, includeCards: false, cancellationToken).ConfigureAwait(false);
            if (deck is null)
                return 0;

            var tournamentEvent = await _eventRepository.GetAsync(eventID, includeMatches: false, cancellationToken).ConfigureAwait(false);
            if (tournamentEvent is null)
                return 0;

            var eventIDs = await GetEventIDsToLinkAsync(tournamentEvent, linkOtherEventsWithSameUrl, cancellationToken).ConfigureAwait(false);
            return await _eventRepository.SetDeckAsync(eventIDs, deckID, cancellationToken).ConfigureAwait(false);
        }

        public DeckImportResult Preview(string? text)
        {
            var (cards, error) = ParseAndBuild(text);
            return cards is null ? DeckImportResult.Failure(error!) : Summarize(cards, deckID: 0, linkedEventCount: 0);
        }

        public async Task<bool> UnlinkEventAsync(int eventID, CancellationToken cancellationToken = default) =>
            await _eventRepository.SetDeckAsync([eventID], null, cancellationToken).ConfigureAwait(false) > 0;

        public async Task<DeckSaveResult> UpdateAsync(int id, string? name, string? notes, CancellationToken cancellationToken = default)
        {
            var normalized = DeckRules.Normalize(new Deck { ID = id, Name = name ?? string.Empty, Notes = notes });

            var errors = DeckRules.Validate(normalized);
            if (errors.Count > 0)
                return DeckSaveResult.Failure(errors);

            var updated = await _deckRepository.UpdateAsync(normalized, cancellationToken).ConfigureAwait(false);
            return updated ? DeckSaveResult.Success() : DeckSaveResult.Missing("Deck not found.");
        }

        private static int CountCopies(IEnumerable<DeckCard> cards, DeckSection section) =>
            cards.Where(c => c.Section == section).Sum(c => c.Quantity);

        private DeckSectionViewModel BuildSection(Deck deck, DeckSection section) =>
            new()
            {
                Cards = DeckSorter.Sort(deck.Cards
                    .Where(c => c.Section == section)
                    .Select(c => new DeckCardViewModel
                    {
                        Card = _cardDataRepository.GetCardByID(c.CardID),
                        CardID = c.CardID,
                        Quantity = c.Quantity
                    }))
            };

        private async Task<IReadOnlyList<int>> GetEventIDsToLinkAsync(Event tournamentEvent, bool includeOthersWithSameUrl, CancellationToken cancellationToken)
        {
            var eventIDs = new List<int> { tournamentEvent.ID };
            if (!includeOthersWithSameUrl || string.IsNullOrWhiteSpace(tournamentEvent.DecklistURL))
                return eventIDs;

            var others = await _eventRepository
                .GetUnlinkedByDecklistUrlAsync(tournamentEvent.DecklistURL, tournamentEvent.ID, cancellationToken)
                .ConfigureAwait(false);
            eventIDs.AddRange(others.Select(e => e.ID));
            return eventIDs;
        }

        private (IReadOnlyList<DeckCard>? Cards, string? Error) ParseAndBuild(string? text)
        {
            var parsed = DeckListParser.Parse(text);
            if (!parsed.Succeeded)
                return (null, parsed.Error);

            return (DeckListBuilder.Build(parsed.Deck!, _cardDataRepository.GetPasscodeAliases()), null);
        }

        private DeckImportResult Summarize(IReadOnlyList<DeckCard> cards, int deckID, int linkedEventCount) =>
            new()
            {
                DeckID = deckID,
                ExtraCount = CountCopies(cards, DeckSection.Extra),
                LinkedEventCount = linkedEventCount,
                MainCount = CountCopies(cards, DeckSection.Main),
                SideCount = CountCopies(cards, DeckSection.Side),
                UnknownPasscodes = cards
                    .Select(c => c.CardID)
                    .Distinct()
                    .Where(cardID => _cardDataRepository.GetCardByID(cardID) is null)
                    .ToList()
            };
    }
}
