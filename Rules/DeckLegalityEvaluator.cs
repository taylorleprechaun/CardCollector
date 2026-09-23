using CardCollector.Models;
using CardCollector.ViewModels;

namespace CardCollector.Rules
{
    /// <summary>
    /// Pure evaluation of a deck against one banlist. Copies are counted across all three sections and grouped by
    /// card name rather than <c>Card.ID</c>, so a card split across multiple passcodes (alt art, or one of the 17
    /// cards with two primary passcodes) can't dodge its limit. A group with no Konami ID is unrestricted. Doesn't
    /// validate deck size.
    /// </summary>
    public static class DeckLegalityEvaluator
    {
        /// <summary>
        /// Returns the status of every restricted card in the deck, keyed by <see cref="DeckCardViewModel.CardID"/>.
        /// Cards within their limit are included too, so the viewer can badge them.
        /// </summary>
        public static IReadOnlyDictionary<int, DeckLegalityCardStatus> Evaluate(IEnumerable<DeckCardViewModel> cards, Banlist banlist)
        {
            if (cards is null) throw new ArgumentNullException(nameof(cards));
            if (banlist is null) throw new ArgumentNullException(nameof(banlist));

            var cardStatuses = new Dictionary<int, DeckLegalityCardStatus>();

            foreach (var group in cards.GroupBy(GetGroupKey))
            {
                var konamiID = group.Select(c => c.Card?.KonamiID).FirstOrDefault(id => id.HasValue);
                if (konamiID is null)
                    continue;

                var limit = banlist.GetLimit(konamiID.Value);
                if (limit == BanlistLimit.Unlimited)
                    continue;

                var status = new DeckLegalityCardStatus { IsViolation = group.Sum(c => c.Quantity) > (int)limit, Limit = limit };
                foreach (var card in group)
                    cardStatuses[card.CardID] = status;
            }

            return cardStatuses;
        }

        private static string GetGroupKey(DeckCardViewModel card) =>
            card.Card?.Name ?? $"Unknown passcode {card.CardID}";
    }
}
