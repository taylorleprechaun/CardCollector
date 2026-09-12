using CardCollector.Data.Models;

namespace CardCollector.ViewModels
{
    public sealed class EditionAuditResult : CardPrinting
    {
        public IReadOnlyList<CardEdition> AvailableEditions { get; init; } = [];

        public EditionAuditCategory Category { get; init; }

        public int CollectionEntryID { get; init; }

        public CardEdition RecordedEdition { get; init; }

        public IReadOnlyList<string> SuggestedPrintVariants { get; init; } = [];

        public static EditionAuditResult From(
            CardPrinting printing,
            int collectionEntryID,
            CardEdition recordedEdition,
            IReadOnlyList<CardEdition> availableEditions,
            EditionAuditCategory category,
            IReadOnlyList<string> suggestedPrintVariants) => new()
        {
            AvailableEditions = availableEditions,
            AvailableRarities = printing.AvailableRarities,
            CardID = printing.CardID,
            CardName = printing.CardName,
            CardType = printing.CardType,
            Category = category,
            CollectionEntryID = collectionEntryID,
            ImageURLSmall = printing.ImageURLSmall,
            Price = printing.Price,
            PrintVariant = printing.PrintVariant,
            RarityCode = printing.RarityCode,
            RarityName = printing.RarityName,
            RecordedEdition = recordedEdition,
            SetCode = printing.SetCode,
            SetName = printing.SetName,
            SuggestedPrintVariants = suggestedPrintVariants
        };
    }
}
