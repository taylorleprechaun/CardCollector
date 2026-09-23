namespace CardCollector.Models
{
    /// <summary>A card whose copies in the deck exceed what its restriction allows.</summary>
    public sealed class DeckLegalityViolation
    {
        public required string CardName { get; init; }

        public required int Copies { get; init; }

        public required BanlistLimit Limit { get; init; }
    }
}
