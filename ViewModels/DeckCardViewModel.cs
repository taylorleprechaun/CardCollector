using CardCollector.DTO;

namespace CardCollector.ViewModels
{
    /// <summary>A stored deck row with the card it refers to, or none when the card data doesn't know that passcode.</summary>
    public sealed class DeckCardViewModel
    {
        public required Card? Card { get; init; }

        public string? ImageURL => Card?.CardImages?.FirstOrDefault()?.ImageURLSmall;

        public bool IsUnknown => Card is null;

        /// <summary>The passcode stored for the row (as pasted). Link to the card with <see cref="Card"/>'s id, not this.</summary>
        public required int PastedPasscode { get; init; }

        public required int Quantity { get; init; }
    }
}
