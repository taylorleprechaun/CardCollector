using CardCollector.ViewModels;

namespace CardCollector.Rules
{
    /// <summary>
    /// Puts a deck section's cards in one standard order, whatever order the list was pasted in: main deck monsters,
    /// then fusion, synchro, xyz and link monsters, then spells, then traps, and by name within each of those.
    /// That single ordering gives every section its rule: main is monsters, spells, traps; extra is fusion, synchro,
    /// xyz, link; side is main deck monsters, extra deck monsters, spells, traps.
    /// </summary>
    public static class DeckSorter
    {
        private const int FUSION = 1;
        private const int LINK = 4;
        private const int MAIN_MONSTER = 0;
        private const int SPELL = 5;
        private const int SYNCHRO = 2;
        private const int TRAP = 6;
        private const int UNKNOWN = int.MaxValue;
        private const int XYZ = 3;

        /// <summary>
        /// Returns the cards sorted by type then name. A card the card data doesn't know goes last, ordered by its passcode.
        /// </summary>
        public static IReadOnlyList<DeckCardViewModel> Sort(IEnumerable<DeckCardViewModel> cards)
        {
            if (cards is null) throw new ArgumentNullException(nameof(cards));

            return cards
                .OrderBy(GetRank)
                .ThenBy(c => c.Card?.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(c => c.CardID)
                .ToList();
        }

        private static int GetRank(DeckCardViewModel card)
        {
            if (card.Card is null) return UNKNOWN;

            var cardType = card.Card.CardType;
            if (DeckRules.IsSpell(cardType)) return SPELL;
            if (DeckRules.IsTrap(cardType)) return TRAP;
            if (DeckRules.HasTypeKeyword(cardType, "Fusion")) return FUSION;
            if (DeckRules.HasTypeKeyword(cardType, "Synchro")) return SYNCHRO;
            if (DeckRules.HasTypeKeyword(cardType, "Xyz")) return XYZ;
            if (DeckRules.HasTypeKeyword(cardType, "Link")) return LINK;

            return MAIN_MONSTER;
        }
    }
}
