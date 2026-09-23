using CardCollector.Data;
using CardCollector.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace CardCollector.Repository
{
    public sealed class MatchRepository : IMatchRepository
    {
        private const int MAX_OPPONENT_DECKS = 200;

        private readonly AppDBContext _context;

        public MatchRepository(AppDBContext context)
        {
            _context = context;
        }

        public async Task<(int Position, IReadOnlyList<Match> Rounds)?> AddAsync(
            int eventID,
            Match match,
            Func<IReadOnlyList<Match>, int> choosePosition,
            CancellationToken cancellationToken = default)
        {
            if (match is null) throw new ArgumentNullException(nameof(match));
            if (choosePosition is null) throw new ArgumentNullException(nameof(choosePosition));

            var tournamentEvent = await _context.Events
                .Include(e => e.Matches)
                .FirstOrDefaultAsync(e => e.ID == eventID, cancellationToken)
                .ConfigureAwait(false);

            if (tournamentEvent is null)
                return null;

            // Sorted here rather than in the Include: an Include doesn't reorder a collection the context already tracks.
            var rounds = tournamentEvent.Matches.OrderBy(m => m.Sequence).ThenBy(m => m.ID).ToList();
            var index = Math.Clamp(choosePosition(rounds), 0, rounds.Count);

            var now = DateTime.UtcNow;
            var entity = new Match
            {
                DateCreated = now,
                DateModified = now,
                EventID = eventID
            };
            CopyFields(match, entity);

            rounds.Insert(index, entity);
            Renumber(rounds);

            _context.Matches.Add(entity);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return (index, rounds);
        }

        public async Task<IReadOnlyList<Match>?> DeleteAsync(int eventID, int id, CancellationToken cancellationToken = default)
        {
            var rounds = await LoadRoundsForUpdateAsync(eventID, cancellationToken).ConfigureAwait(false);

            var target = rounds.FirstOrDefault(m => m.ID == id);
            if (target is null)
                return null;

            rounds.Remove(target);
            _context.Matches.Remove(target);
            Renumber(rounds);

            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return rounds;
        }

        public async Task<IReadOnlyList<Match>> GetByEventAsync(int eventID, CancellationToken cancellationToken = default) =>
            await _context.Matches
                .AsNoTracking()
                .Where(m => m.EventID == eventID)
                .OrderBy(m => m.Sequence)
                .ThenBy(m => m.ID)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        public async Task<IReadOnlyList<string>> GetOpponentDecksAsync(CancellationToken cancellationToken = default) =>
            await _context.Matches
                .AsNoTracking()
                .Where(m => !m.IsBye)
                .GroupBy(m => m.OpponentDeck)
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .ThenBy(g => g.Name)
                .Select(g => g.Name)
                .Take(MAX_OPPONENT_DECKS)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        public async Task<int> SetOrderAsync(int eventID, IReadOnlyList<int> orderedIDs, CancellationToken cancellationToken = default)
        {
            if (orderedIDs is null) throw new ArgumentNullException(nameof(orderedIDs));

            var rounds = await _context.Matches
                .Where(m => m.EventID == eventID)
                .ToDictionaryAsync(m => m.ID, cancellationToken)
                .ConfigureAwait(false);

            var moved = 0;
            var position = 0;
            foreach (var id in orderedIDs)
            {
                if (!rounds.TryGetValue(id, out var round))
                    continue;

                position++;
                if (round.Sequence == position)
                    continue;

                round.Sequence = position;
                moved++;
            }

            if (moved > 0)
                await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return moved;
        }

        public async Task<IReadOnlyList<Match>?> UpdateAsync(int eventID, Match match, CancellationToken cancellationToken = default)
        {
            if (match is null) throw new ArgumentNullException(nameof(match));

            var rounds = await LoadRoundsForUpdateAsync(eventID, cancellationToken).ConfigureAwait(false);

            var entity = rounds.FirstOrDefault(m => m.ID == match.ID);
            if (entity is null)
                return null;

            CopyFields(match, entity);
            entity.DateModified = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return rounds;
        }

        /// <summary>
        /// The owning event and the round's position are managed by the repository, so a form-driven save must not overwrite them.
        /// </summary>
        private static void CopyFields(Match source, Match target)
        {
            target.GamesLost = source.GamesLost;
            target.GamesTied = source.GamesTied;
            target.GamesWon = source.GamesWon;
            target.IsBye = source.IsBye;
            target.Notes = source.Notes;
            target.OpponentDeck = source.OpponentDeck;
            target.Result = source.Result;
            target.Round = source.Round;
            target.WonDiceRoll = source.WonDiceRoll;
        }

        private Task<List<Match>> LoadRoundsForUpdateAsync(int eventID, CancellationToken cancellationToken) =>
            _context.Matches
                .Where(m => m.EventID == eventID)
                .OrderBy(m => m.Sequence)
                .ThenBy(m => m.ID)
                .ToListAsync(cancellationToken);

        /// <summary>Keeps the event's rounds numbered 1 to n in their list order.</summary>
        private static void Renumber(IReadOnlyList<Match> rounds)
        {
            for (var index = 0; index < rounds.Count; index++)
                rounds[index].Sequence = index + 1;
        }
    }
}
