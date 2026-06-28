using CardCollector.Data.Models;

namespace CardCollector.ViewModels
{
    public sealed class OrderEntryViewModel : CardPrinting
    {
        public AcquisitionMethod? AcquisitionMethod { get; init; }

        public CardCondition? Condition { get; init; }

        public DateTime DateCreated { get; init; }

        public CardEdition? Edition { get; init; }

        public int EntryID { get; init; }

        public decimal? MarketPriceAtEntry { get; init; }

        public DateTime? PurchaseDate { get; init; }

        public decimal? PurchasePrice { get; init; }

        public int Quantity { get; init; } = 1;

        public static OrderEntryViewModel From(CardPrinting printing, CollectionEntry entry) => new()
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
            AcquisitionMethod = entry.AcquisitionMethod,
            Condition = entry.Condition,
            DateCreated = entry.DateCreated,
            Edition = entry.Edition,
            EntryID = entry.ID,
            MarketPriceAtEntry = entry.MarketPriceAtEntry,
            PurchaseDate = entry.PurchaseDate,
            PurchasePrice = entry.PurchasePrice,
            Quantity = entry.Quantity
        };
    }
}
