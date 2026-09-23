using CardCollector.Data.Models;
using CardCollector.Models;
using CardCollector.Repository;
using CardCollector.Rules;
using CardCollector.ViewModels;

namespace CardCollector.Services
{
    public sealed class MatchService : IMatchService
    {
        private readonly IEventRepository _eventRepository;
        private readonly IMatchRepository _matchRepository;

        public MatchService(IEventRepository eventRepository, IMatchRepository matchRepository)
        {
            _eventRepository = eventRepository;
            _matchRepository = matchRepository;
        }

        public async Task<MatchSaveResult> AddAsync(int eventID, Match match, CancellationToken cancellationToken = default)
        {
            if (match is null) throw new ArgumentNullException(nameof(match));

            var tournamentEvent = await _eventRepository.GetAsync(eventID, includeMatches: false, cancellationToken).ConfigureAwait(false);
            if (tournamentEvent is null)
                return MatchSaveResult.Missing("Event not found.");

            var normalized = MatchRules.Normalize(match);

            var errors = MatchRules.Validate(normalized);
            if (errors.Count > 0)
                return MatchSaveResult.Failure(errors);

            var existing = await _matchRepository.GetByEventAsync(eventID, cancellationToken).ConfigureAwait(false);
            var position = MatchRules.GetInsertPosition(existing.Select(m => m.Round).ToList(), normalized.Round);

            normalized.ID = await _matchRepository.AddAsync(eventID, normalized, position, cancellationToken).ConfigureAwait(false);
            normalized.EventID = eventID;
            return MatchSaveResult.Success(normalized, position > 0 ? existing[position - 1].ID : null);
        }

        public Task<bool> DeleteAsync(int eventID, int id, CancellationToken cancellationToken = default) =>
            _matchRepository.DeleteAsync(eventID, id, cancellationToken);

        public Task<IReadOnlyList<string>> GetOpponentDecksAsync(CancellationToken cancellationToken = default) =>
            _matchRepository.GetOpponentDecksAsync(cancellationToken);

        public async Task<MatchSummaryViewModel> GetSummaryAsync(int eventID, CancellationToken cancellationToken = default)
        {
            var matches = await _matchRepository.GetByEventAsync(eventID, cancellationToken).ConfigureAwait(false);
            return MatchRules.Summarize(matches);
        }

        public async Task<bool> SortAsync(int eventID, CancellationToken cancellationToken = default)
        {
            var rounds = await _matchRepository.GetByEventAsync(eventID, cancellationToken).ConfigureAwait(false);
            if (MatchRules.IsInRoundOrder(rounds.Select(m => m.Round)))
                return false;

            var ordered = MatchRules.OrderByRound(rounds).Select(m => m.ID).ToList();
            await _matchRepository.SetOrderAsync(eventID, ordered, cancellationToken).ConfigureAwait(false);
            return true;
        }

        public async Task<MatchSaveResult> UpdateAsync(int eventID, Match match, CancellationToken cancellationToken = default)
        {
            if (match is null) throw new ArgumentNullException(nameof(match));

            var normalized = MatchRules.Normalize(match);

            var errors = MatchRules.Validate(normalized);
            if (errors.Count > 0)
                return MatchSaveResult.Failure(errors);

            var updated = await _matchRepository.UpdateAsync(eventID, normalized, cancellationToken).ConfigureAwait(false);
            if (!updated)
                return MatchSaveResult.Missing("Round not found.");

            normalized.EventID = eventID;
            return MatchSaveResult.Success(normalized);
        }
    }
}
