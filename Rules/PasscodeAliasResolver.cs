using CardCollector.DTO;

namespace CardCollector.Rules
{
    /// <summary>
    /// A card can be written with more than one passcode: 17 cards have a different primary id in YGOProDeck than
    /// in yaml-yugi, and any card with alternate artwork has an extra passcode per artwork. Deck lists may use
    /// any of them, so they are mapped to the one <c>Card.ID</c> the app tracks the card by.
    /// </summary>
    public static class PasscodeAliasResolver
    {
        /// <summary>
        /// Maps every alternate passcode to the app's <c>Card.ID</c> for the same card. Within one YGOProDeck card the
        /// canonical id is its primary id if yaml-yugi has it, otherwise the yaml-yugi id among its artwork ids,
        /// otherwise the primary id (a card only YGOProDeck lists). An id that is itself a yaml-yugi card, or another
        /// YGOProDeck card's primary id, is never treated as an alias.
        /// </summary>
        public static IReadOnlyDictionary<int, int> BuildAliases(IEnumerable<int> yamlCardIDs, IEnumerable<Card> ygoProDeckCards)
        {
            if (yamlCardIDs is null) throw new ArgumentNullException(nameof(yamlCardIDs));
            if (ygoProDeckCards is null) throw new ArgumentNullException(nameof(ygoProDeckCards));

            var yamlIDs = yamlCardIDs.ToHashSet();
            var cards = ygoProDeckCards.ToList();
            var primaryIDs = cards.Select(c => c.ID).ToHashSet();
            var aliases = new Dictionary<int, int>();

            foreach (var card in cards)
            {
                var passcodes = new List<int> { card.ID };
                passcodes.AddRange((card.CardImages ?? []).Select(image => image.ID));

                var canonical = passcodes.FirstOrDefault(id => yamlIDs.Contains(id), card.ID);
                foreach (var passcode in passcodes.Distinct())
                {
                    var isOtherCard = passcode != card.ID && primaryIDs.Contains(passcode);
                    if (passcode == canonical || yamlIDs.Contains(passcode) || isOtherCard)
                        continue;

                    aliases.TryAdd(passcode, canonical);
                }
            }

            return aliases;
        }

        /// <summary>Returns the app's <c>Card.ID</c> for the passcode, or the passcode itself when it has no alias.</summary>
        public static int Resolve(IReadOnlyDictionary<int, int> aliases, int passcode)
        {
            if (aliases is null) throw new ArgumentNullException(nameof(aliases));

            return aliases.GetValueOrDefault(passcode, passcode);
        }
    }
}
