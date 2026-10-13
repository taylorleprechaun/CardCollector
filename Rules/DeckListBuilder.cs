using CardCollector.Data.Models;
using CardCollector.Models;

namespace CardCollector.Rules
{
    /// <summary>Turns a parsed deck list into the stored rows: one per distinct card per section.</summary>
    public static class DeckListBuilder
    {
        /// <summary>
        /// Resolves each passcode to its catalog card first, then collapses copies into a quantity, so two passcodes for one
        /// card add up. A row keeps the first passcode pasted for it and the catalog card it resolved to; an unknown
        /// passcode gets a row of its own with no catalog card. The sort order is where the card first appeared in its
        /// section.
        /// </summary>
        /// <param name="deck">The parsed deck list.</param>
        /// <param name="catalogCardIDsByPasscode">Every known passcode mapped to its catalog card id.</param>
        /// <returns>The deck rows to store.</returns>
        public static IReadOnlyList<DeckCard> Build(ParsedDeckList deck, IReadOnlyDictionary<int, int> catalogCardIDsByPasscode)
        {
            if (deck is null) throw new ArgumentNullException(nameof(deck));
            if (catalogCardIDsByPasscode is null) throw new ArgumentNullException(nameof(catalogCardIDsByPasscode));

            var rows = new List<DeckCard>();
            AddSection(rows, DeckSection.Main, deck.Main, catalogCardIDsByPasscode);
            AddSection(rows, DeckSection.Extra, deck.Extra, catalogCardIDsByPasscode);
            AddSection(rows, DeckSection.Side, deck.Side, catalogCardIDsByPasscode);
            return rows;
        }

        private static void AddSection(
            ICollection<DeckCard> rows,
            DeckSection section,
            IReadOnlyList<int> passcodes,
            IReadOnlyDictionary<int, int> catalogCardIDsByPasscode)
        {
            var rowsByCard = new Dictionary<(int? CatalogCardID, int UnknownPasscode), DeckCard>();

            for (var index = 0; index < passcodes.Count; index++)
            {
                var passcode = passcodes[index];
                int? catalogCardID = catalogCardIDsByPasscode.TryGetValue(passcode, out var id) ? id : null;
                var key = catalogCardID is null ? ((int?)null, passcode) : (catalogCardID, 0);
                if (rowsByCard.TryGetValue(key, out var existing))
                {
                    existing.Quantity++;
                    continue;
                }

                var row = new DeckCard { CardID = passcode, CatalogCardID = catalogCardID, Quantity = 1, Section = section, SortOrder = index };
                rowsByCard.Add(key, row);
                rows.Add(row);
            }
        }
    }
}
