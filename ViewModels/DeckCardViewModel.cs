using CardCollector.DTO;

namespace CardCollector.ViewModels
{
    /// <summary>A stored deck row with the card it refers to, or none when the card data doesn't know that passcode.</summary>
    public sealed class DeckCardViewModel
    {
        public required Card? Card { get; init; }

        public required int CardID { get; init; }

        public string? ImageURL => Card?.CardImages?.FirstOrDefault()?.ImageURLSmall;

        public bool IsUnknown => Card is null;

        public required int Quantity { get; init; }
    }
}
