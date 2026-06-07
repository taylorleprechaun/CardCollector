namespace CardCollector.ViewModels
{
    public sealed class WishlistItemViewModel : CardPrinting
    {
        public int QuantityNeeded => CollectionGroupViewModel.CompleteThreshold - QuantityOwned;

        public int QuantityOwned { get; init; } = 0;

        public static WishlistItemViewModel From(CardPrinting printing, int quantityOwned = 0) => new()
        {
            AvailableRarities = printing.AvailableRarities,
            CardID = printing.CardID,
            CardName = printing.CardName,
            ImageID = printing.ImageID,
            ImageURLSmall = printing.ImageURLSmall,
            Price = printing.Price,
            QuantityOwned = quantityOwned,
            RarityCode = printing.RarityCode,
            RarityName = printing.RarityName,
            SetCode = printing.SetCode,
            SetName = printing.SetName
        };
    }
}
