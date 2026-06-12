namespace CardCollector.ViewModels
{
    public sealed class CheckedOutCardViewModel : CardPrinting
    {
        public DateTime CheckedOutDate { get; init; }

        public int CheckedOutQuantity { get; init; }

        public int TotalOwnedQuantity { get; init; }

        public static CheckedOutCardViewModel From(CardPrinting printing, DateTime checkedOutDate, int checkedOutQuantity, int totalOwnedQuantity) => new()
        {
            AvailableRarities = printing.AvailableRarities,
            CardID = printing.CardID,
            CardName = printing.CardName,
            CardType = printing.CardType,
            CheckedOutDate = checkedOutDate,
            CheckedOutQuantity = checkedOutQuantity,
            ImageID = printing.ImageID,
            ImageURLSmall = printing.ImageURLSmall,
            Price = printing.Price,
            RarityCode = printing.RarityCode,
            RarityName = printing.RarityName,
            SetCode = printing.SetCode,
            SetName = printing.SetName,
            TotalOwnedQuantity = totalOwnedQuantity
        };
    }
}
