namespace CardCollector.Services
{
    /// <summary>The result of checking a deck's cards against one banlist.</summary>
    public sealed class DeckLegality
    {
        /// <summary>Status for every restricted card in the deck, keyed by <c>DeckCardViewModel.CardID</c> — legal ones included, so the viewer can badge them too.</summary>
        public required IReadOnlyDictionary<int, DeckLegalityCardStatus> CardStatuses { get; init; }

        public required DateOnly EffectiveDate { get; init; }

        public bool IsLegal => Violations.Count == 0;

        public required IReadOnlyList<DeckLegalityViolation> Violations { get; init; }
    }

    /// <summary>A restricted card's status against the deck's copy count, whether or not it violates the limit.</summary>
    public sealed class DeckLegalityCardStatus
    {
        public required bool IsViolation { get; init; }

        public required BanlistLimit Limit { get; init; }
    }

    /// <summary>A card whose copies in the deck exceed what its restriction allows.</summary>
    public sealed class DeckLegalityViolation
    {
        public required string CardName { get; init; }

        public required int Copies { get; init; }

        public required BanlistLimit Limit { get; init; }
    }
}
