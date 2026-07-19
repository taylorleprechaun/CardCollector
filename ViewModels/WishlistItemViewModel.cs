namespace CardCollector.ViewModels
{
    public sealed class WishlistItemViewModel : CardPrinting
    {
        public int CartQuantity { get; init; }

        public bool IsInCart => CartQuantity > 0;

        public bool IsOrdered => OrderedQuantity > 0;

        public int OrderedQuantity { get; init; }

        public int QuantityNeeded => CompleteThreshold - QuantityOwned;

        public int QuantityOwned { get; init; } = 0;

        public static WishlistItemViewModel From(CardPrinting printing, int quantityOwned = 0, int cartQuantity = 0, int orderedQuantity = 0) => new()
        {
            AvailableRarities = printing.AvailableRarities,
            CardID = printing.CardID,
            CardName = printing.CardName,
            CardType = printing.CardType,
            CartQuantity = cartQuantity,
            ImageID = printing.ImageID,
            ImageURLSmall = printing.ImageURLSmall,
            OrderedQuantity = orderedQuantity,
            Price = printing.Price,
            QuantityOwned = quantityOwned,
            RarityCode = printing.RarityCode,
            RarityName = printing.RarityName,
            SetCode = printing.SetCode,
            SetName = printing.SetName
        };
    }
}
