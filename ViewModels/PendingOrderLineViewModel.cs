using CardCollector.Data.Models;

namespace CardCollector.ViewModels
{
    public sealed class PendingOrderLineViewModel : CardPrinting
    {
        public AcquisitionMethod? AcquisitionMethod { get; init; }

        public CardCondition? Condition { get; init; }

        public DateTime DateCreated { get; init; }

        public CardEdition? Edition { get; init; }

        public decimal? MarketPriceAtEntry { get; init; }

        public int PendingOrderLineID { get; init; }

        public DateTime? PurchaseDate { get; init; }

        public decimal? PurchasePrice { get; init; }

        public int Quantity { get; init; } = 1;

        public static PendingOrderLineViewModel From(CardPrinting printing, PendingOrderLine line) => new()
        {
            AvailableRarities = printing.AvailableRarities,
            CardID = printing.CardID,
            CardName = printing.CardName,
            CardType = printing.CardType,
            ImageID = printing.ImageID,
            ImageURLSmall = printing.ImageURLSmall,
            Price = printing.Price,
            RarityCode = printing.RarityCode,
            RarityName = printing.RarityName,
            SetCode = printing.SetCode,
            SetName = printing.SetName,
            AcquisitionMethod = line.AcquisitionMethod,
            Condition = line.Condition,
            DateCreated = line.DateCreated,
            Edition = line.Edition,
            MarketPriceAtEntry = line.MarketPriceAtEntry,
            PendingOrderLineID = line.ID,
            PurchaseDate = line.PurchaseDate,
            PurchasePrice = line.PurchasePrice,
            Quantity = line.Quantity
        };
    }
}
