using CardCollector.Data.Models;
using CardCollector.Models;
using CardCollector.Repository;
using CardCollector.Rules;
using CardCollector.ViewModels;

namespace CardCollector.Services
{
    public sealed class MatchService : IMatchService
    {
        private readonly IMatchRepository _matchRepository;

        public MatchService(IMatchRepository matchRepository)
        {
            _matchRepository = matchRepository;
        }

        public async Task<MatchSaveResult> AddAsync(int eventID, Match match, CancellationToken cancellationToken = default)
        {
            if (match is null) throw new ArgumentNullException(nameof(match));

            var normalized = MatchRules.Normalize(match);

            var errors = MatchRules.Validate(normalized);
            if (errors.Count > 0)
                return MatchSaveResult.Failure(errors);

            var added = await _matchRepository
                .AddAsync(eventID, normalized, rounds => MatchRules.GetInsertPosition(rounds.Select(m => m.Round).ToList(), normalized.Round), cancellationToken)
                .ConfigureAwait(false);

            if (added is not { } result)
                return MatchSaveResult.Missing("Event not found.");

            var (position, rounds) = result;

            var previousMatchID = position > 0 ? rounds[position - 1].ID : (int?)null;
            return MatchSaveResult.Success(rounds[position], MatchRules.Summarize(rounds), previousMatchID);
        }

        public async Task<MatchSummaryViewModel?> DeleteAsync(int eventID, int id, CancellationToken cancellationToken = default)
        {
            var remaining = await _matchRepository.DeleteAsync(eventID, id, cancellationToken).ConfigureAwait(false);
            return remaining is null ? null : MatchRules.Summarize(remaining);
        }

        public Task<IReadOnlyList<string>> GetOpponentDecksAsync(CancellationToken cancellationToken = default) =>
            _matchRepository.GetOpponentDecksAsync(cancellationToken);

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

            var rounds = await _matchRepository.UpdateAsync(eventID, normalized, cancellationToken).ConfigureAwait(false);
            if (rounds is null)
                return MatchSaveResult.Missing("Round not found.");

            return MatchSaveResult.Success(rounds.First(m => m.ID == normalized.ID), MatchRules.Summarize(rounds));
        }
    }
}
