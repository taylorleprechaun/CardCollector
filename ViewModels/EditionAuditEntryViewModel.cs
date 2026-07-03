using CardCollector.Data.Models;

namespace CardCollector.ViewModels
{
    public sealed class EditionAuditEntryViewModel : OrderEntryViewModel
    {
        public IReadOnlyList<CardEdition> AvailableEditions { get; init; } = [];

        public EditionAuditCategory? Category { get; init; }

        public static EditionAuditEntryViewModel From(
            OrderEntryViewModel entry,
            EditionAuditCategory? category,
            IReadOnlyList<CardEdition> availableEditions) => new()
        {
            AcquisitionMethod = entry.AcquisitionMethod,
            AvailableEditions = availableEditions,
            AvailableRarities = entry.AvailableRarities,
            CardID = entry.CardID,
            CardName = entry.CardName,
            CardType = entry.CardType,
            Category = category,
            Condition = entry.Condition,
            DateCreated = entry.DateCreated,
            Edition = entry.Edition,
            EntryID = entry.EntryID,
            ImageID = entry.ImageID,
            ImageURLSmall = entry.ImageURLSmall,
            MarketPriceAtEntry = entry.MarketPriceAtEntry,
            Price = entry.Price,
            PurchaseDate = entry.PurchaseDate,
            PurchasePrice = entry.PurchasePrice,
            Quantity = entry.Quantity,
            RarityCode = entry.RarityCode,
            RarityName = entry.RarityName,
            SetCode = entry.SetCode,
            SetName = entry.SetName
        };
    }
}
