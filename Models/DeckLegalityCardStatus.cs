namespace CardCollector.Models
{
    /// <summary>A restricted card's status against the deck's copy count, whether or not it violates the limit.</summary>
    public sealed class DeckLegalityCardStatus
    {
        public required bool IsViolation { get; init; }

        public required BanlistLimit Limit { get; init; }
    }
}
