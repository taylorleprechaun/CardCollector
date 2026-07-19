namespace CardCollector.ViewModels
{
    public class CardPrinting
    {
        public const int CompleteThreshold = 3;

        public IReadOnlyList<string> AvailableRarities { get; init; } = [];

        public int CardID { get; init; }

        public string CardName { get; init; } = string.Empty;

        public string CardType { get; init; } = string.Empty;

        public int ImageID { get; init; }

        public string ImageURLSmall { get; init; } = string.Empty;

        public decimal? Price { get; init; }

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
            ImageID = ImageID,
            ImageURLSmall = ImageURLSmall,
            Price = price,
            RarityCode = RarityCode,
            RarityName = RarityName,
            SetCode = SetCode,
            SetName = SetName
        };
    }
}
