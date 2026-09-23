using CardCollector.Data.Models;
using CardCollector.Models;

namespace CardCollector.Rules
{
    /// <summary>Turns a parsed deck list into the stored rows: one per distinct card per section.</summary>
    public static class DeckListBuilder
    {
        /// <summary>
        /// Resolves each passcode to the app's card id first, then collapses copies into a quantity, so two
        /// passcodes for one card add up. The sort order is where the card first appeared in its section.
        /// </summary>
        public static IReadOnlyList<DeckCard> Build(ParsedDeckList deck, IReadOnlyDictionary<int, int> aliases)
        {
            if (deck is null) throw new ArgumentNullException(nameof(deck));
            if (aliases is null) throw new ArgumentNullException(nameof(aliases));

            var rows = new List<DeckCard>();
            AddSection(rows, DeckSection.Main, deck.Main, aliases);
            AddSection(rows, DeckSection.Extra, deck.Extra, aliases);
            AddSection(rows, DeckSection.Side, deck.Side, aliases);
            return rows;
        }

        private static void AddSection(List<DeckCard> rows, DeckSection section, IReadOnlyList<int> passcodes, IReadOnlyDictionary<int, int> aliases)
        {
            var rowsByCardID = new Dictionary<int, DeckCard>();

            for (var index = 0; index < passcodes.Count; index++)
            {
                var cardID = PasscodeAliasResolver.Resolve(aliases, passcodes[index]);
                if (rowsByCardID.TryGetValue(cardID, out var existing))
                {
                    existing.Quantity++;
                    continue;
                }

                var row = new DeckCard { CardID = cardID, Quantity = 1, Section = section, SortOrder = index };
                rowsByCardID.Add(cardID, row);
                rows.Add(row);
            }
        }
    }
}
