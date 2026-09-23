using CardCollector.ViewModels;

namespace CardCollector.Services
{
    /// <summary>
    /// Pure evaluation of a deck against one banlist. Copies are counted across all three sections and grouped by
    /// card name rather than <c>Card.ID</c>, so a card split across multiple passcodes (alt art, or one of the 17
    /// cards with two primary passcodes) can't dodge its limit. A group with no Konami ID is unrestricted. Doesn't
    /// validate deck size.
    /// </summary>
    public static class DeckLegalityEvaluator
    {
        public static DeckLegality Evaluate(IEnumerable<DeckCardViewModel> cards, Banlist banlist)
        {
            if (cards is null) throw new ArgumentNullException(nameof(cards));
            if (banlist is null) throw new ArgumentNullException(nameof(banlist));

            var cardStatuses = new Dictionary<int, DeckLegalityCardStatus>();
            var violations = new List<DeckLegalityViolation>();

            foreach (var group in cards.GroupBy(GetGroupKey))
            {
                var konamiID = group.Select(c => c.Card?.KonamiID).FirstOrDefault(id => id.HasValue);
                if (konamiID is null)
                    continue;

                var limit = banlist.GetLimit(konamiID.Value);
                if (limit == BanlistLimit.Unlimited)
                    continue;

                var copies = group.Sum(c => c.Quantity);
                var isViolation = copies > (int)limit;
                var status = new DeckLegalityCardStatus { IsViolation = isViolation, Limit = limit };

                foreach (var card in group)
                    cardStatuses[card.CardID] = status;

                if (isViolation)
                {
                    violations.Add(new DeckLegalityViolation
                    {
                        CardName = group.Key,
                        Copies = copies,
                        Limit = limit
                    });
                }
            }

            return new DeckLegality
            {
                CardStatuses = cardStatuses,
                EffectiveDate = banlist.EffectiveDate,
                Violations = violations
                    .OrderBy(v => v.Limit)
                    .ThenBy(v => v.CardName, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };
        }

        private static string GetGroupKey(DeckCardViewModel card) =>
            card.Card?.Name ?? $"Unknown passcode {card.CardID}";
    }
}
