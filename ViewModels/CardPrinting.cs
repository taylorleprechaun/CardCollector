namespace CardCollector.ViewModels
{
    public class CardPrinting
    {
        public IReadOnlyList<string> AvailableRarities { get; init; } = [];

        public int CardID { get; init; }

        public string CardName { get; init; } = string.Empty;

        public string CardType { get; init; } = string.Empty;

        public int CompleteThreshold { get; init; } = 3;

        public string ImageURLSmall { get; init; } = string.Empty;

        public decimal? Price { get; init; }

        /// <summary>The catalog printing; 0 when the row's printing isn't in the catalog.</summary>
        public int PrintingID { get; init; }

        /// <summary>Null for a normal/base print; a distinct sellable variant of the same rarity otherwise (e.g. "Extended Art").</summary>
        public string? PrintVariant { get; init; }

        public string RarityCode { get; init; } = string.Empty;

        public string RarityName { get; init; } = string.Empty;

        public string SetCode { get; init; } = string.Empty;

        public string SetName { get; init; } = string.Empty;

        public CardPrinting WithPrice(decimal? price) => new()
        {
            AvailableRarities = AvailableRarities,
            CardID = CardID,
            CardName = CardName,
            CardType = CardType,
            ImageURLSmall = ImageURLSmall,
            Price = price,
            PrintingID = PrintingID,
            PrintVariant = PrintVariant,
            RarityCode = RarityCode,
            RarityName = RarityName,
            SetCode = SetCode,
            SetName = SetName
        };
    }
}
