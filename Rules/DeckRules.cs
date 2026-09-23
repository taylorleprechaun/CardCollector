using CardCollector.Data.Models;
using CardCollector.ViewModels;

namespace CardCollector.Rules
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
                known.Where(c => !IsSpell(c.Card?.CardType) && !IsTrap(c.Card?.CardType)).Sum(c => c.Quantity),
                known.Where(c => IsSpell(c.Card?.CardType)).Sum(c => c.Quantity),
                known.Where(c => IsTrap(c.Card?.CardType)).Sum(c => c.Quantity));
        }

        /// <summary>True when the card type (for example "Spell Card") is a spell.</summary>
        public static bool IsSpell(string? cardType) =>
            cardType?.Contains("Spell", StringComparison.OrdinalIgnoreCase) == true;

        /// <summary>True when the card type (for example "Trap Card") is a trap.</summary>
        public static bool IsTrap(string? cardType) =>
            cardType?.Contains("Trap", StringComparison.OrdinalIgnoreCase) == true;

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
    }
}
