using CardCollector.Data.Models;

namespace CardCollector.ViewModels
{
    public class CollectionGroupViewModel : CardPrinting
    {
        private const int COMPLETE_THRESHOLD = 3;

        public CollectionCompletionStatus CompletionStatus =>
            !IsPreferredVersion
                ? CollectionCompletionStatus.Placeholder
                : TotalQuantity >= COMPLETE_THRESHOLD
                    ? CollectionCompletionStatus.Complete
                    : CollectionCompletionStatus.Incomplete;

        public IList<OrderEntryViewModel> Entries { get; init; } = [];

        public bool IsPreferredVersion { get; init; }

        public decimal? TotalCost { get; init; }

        public int TotalQuantity { get; init; }

        public static CollectionGroupViewModel From(
            CardPrinting printing,
            IList<OrderEntryViewModel> entries,
            bool isPreferredVersion,
            decimal? totalCost,
            int totalQuantity) => new()
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
            SetName = printing.SetName,
            Entries = entries,
            IsPreferredVersion = isPreferredVersion,
            TotalCost = totalCost,
            TotalQuantity = totalQuantity
        };
    }
}
