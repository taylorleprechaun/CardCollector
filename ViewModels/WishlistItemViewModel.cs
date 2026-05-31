namespace CardCollector.ViewModels
{
    public sealed class WishlistItemViewModel : CardPrinting
    {
        public static WishlistItemViewModel From(CardPrinting printing) => new()
        {
            AvailableRarities = printing.AvailableRarities,
            CardID = printing.CardID,
            CardName = printing.CardName,
            ImageID = printing.ImageID,
            ImageURLSmall = printing.ImageURLSmall,
            Price = printing.Price,
            RarityCode = printing.RarityCode,
            RarityName = printing.RarityName,
            SetCode = printing.SetCode,
            SetName = printing.SetName
        };
    }
}
