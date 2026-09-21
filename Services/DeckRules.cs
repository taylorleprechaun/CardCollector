using CardCollector.Data.Models;
using CardCollector.ViewModels;

namespace CardCollector.Services
{
    /// <summary>Pure validation, normalization and tally rules for <see cref="Deck"/>.</summary>
    public static class DeckRules
    {
        public const int MAX_NAME_LENGTH = 150;

        /// <summary>Counts the copies of each known card by type. Cards the card data doesn't know are skipped.</summary>
        public static DeckTypeCounts CountTypes(IEnumerable<DeckCardViewModel> cards)
        {
            if (cards is null) throw new ArgumentNullException(nameof(cards));

            var known = cards.Where(c => c.Card is not null).ToList();
            return new DeckTypeCounts(
                known.Where(c => !IsSpell(c) && !IsTrap(c)).Sum(c => c.Quantity),
                known.Where(IsSpell).Sum(c => c.Quantity),
                known.Where(IsTrap).Sum(c => c.Quantity));
        }

        /// <summary>
        /// Returns a copy of the deck's own fields with text trimmed and blank notes turned into null.
        /// The cards are not copied.
        /// </summary>
        public static Deck Normalize(Deck deck)
        {
            if (deck is null) throw new ArgumentNullException(nameof(deck));

            var notes = deck.Notes?.Trim();
            return new Deck
            {
                ID = deck.ID,
                Name = deck.Name?.Trim() ?? string.Empty,
                Notes = string.IsNullOrEmpty(notes) ? null : notes
            };
        }

        /// <summary>Validates an already-normalized deck.</summary>
        public static IReadOnlyList<string> Validate(Deck deck)
        {
            if (deck is null) throw new ArgumentNullException(nameof(deck));

            if (string.IsNullOrWhiteSpace(deck.Name))
                return ["Deck name is required."];

            if (deck.Name.Length > MAX_NAME_LENGTH)
                return [$"Deck name must be {MAX_NAME_LENGTH} characters or fewer."];

            return [];
        }

        private static bool IsSpell(DeckCardViewModel card) =>
            card.Card?.CardType?.Contains("Spell", StringComparison.OrdinalIgnoreCase) == true;

        private static bool IsTrap(DeckCardViewModel card) =>
            card.Card?.CardType?.Contains("Trap", StringComparison.OrdinalIgnoreCase) == true;
    }
}
