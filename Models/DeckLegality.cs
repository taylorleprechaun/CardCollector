namespace CardCollector.Models
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
}
