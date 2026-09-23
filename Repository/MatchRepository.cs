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

        public async Task<int> AddAsync(int eventID, Match match, int? position = null, CancellationToken cancellationToken = default)
        {
            if (match is null) throw new ArgumentNullException(nameof(match));

            var rounds = await _context.Matches
                .Where(m => m.EventID == eventID)
                .OrderBy(m => m.Sequence)
                .ThenBy(m => m.ID)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var index = Math.Clamp(position ?? rounds.Count, 0, rounds.Count);
            for (var current = 0; current < rounds.Count; current++)
                rounds[current].Sequence = current < index ? current + 1 : current + 2;

            var now = DateTime.UtcNow;
            var entity = new Match
            {
                DateCreated = now,
                DateModified = now,
                EventID = eventID,
                Sequence = index + 1
            };
            CopyFields(match, entity);

            _context.Matches.Add(entity);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return entity.ID;
        }

        public async Task<bool> DeleteAsync(int eventID, int id, CancellationToken cancellationToken = default)
        {
            var rounds = await _context.Matches
                .Where(m => m.EventID == eventID)
                .OrderBy(m => m.Sequence)
                .ThenBy(m => m.ID)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var target = rounds.FirstOrDefault(m => m.ID == id);
            if (target is null)
                return false;

            rounds.Remove(target);
            _context.Matches.Remove(target);

            for (var index = 0; index < rounds.Count; index++)
                rounds[index].Sequence = index + 1;

            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }

        public async Task<Match?> GetAsync(int eventID, int id, CancellationToken cancellationToken = default) =>
            await _context.Matches
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.EventID == eventID && m.ID == id, cancellationToken)
                .ConfigureAwait(false);

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

        public async Task<bool> UpdateAsync(int eventID, Match match, CancellationToken cancellationToken = default)
        {
            if (match is null) throw new ArgumentNullException(nameof(match));

            var entity = await _context.Matches
                .FirstOrDefaultAsync(m => m.EventID == eventID && m.ID == match.ID, cancellationToken)
                .ConfigureAwait(false);

            if (entity is null)
                return false;

            CopyFields(match, entity);
            entity.DateModified = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
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
    }
}
