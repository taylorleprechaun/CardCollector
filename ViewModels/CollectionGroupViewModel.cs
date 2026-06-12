using CardCollector.Data.Models;

namespace CardCollector.ViewModels
{
    public sealed class CollectionGroupViewModel : CardPrinting
    {
        public DateTime? CheckedOutDate { get; init; }

        public int CheckedOutQuantity { get; init; }

        public CollectionCompletionStatus CompletionStatus
        {
            get
            {
                if (!IsPreferredVersion)
                    return CollectionCompletionStatus.Placeholder;

                var nonPlaceholderQty = Entries.Where(e => !e.IsPlaceholder).Sum(e => e.Quantity);

                if (nonPlaceholderQty >= CompleteThreshold || (nonPlaceholderQty > 0 && HasAnyPlaceholderForImage))
                    return CollectionCompletionStatus.Complete;

                return CollectionCompletionStatus.Incomplete;
            }
        }

        public IReadOnlyList<OrderEntryViewModel> Entries { get; init; } = [];

        public bool HasAnyPlaceholderForImage { get; init; }

        public bool IsCheckedOut => CheckedOutQuantity > 0;

        public bool IsPreferredVersion { get; init; }

        public decimal? TotalCost { get; init; }

        public int TotalQuantity { get; init; }

        public static CollectionGroupViewModel From(
            CardPrinting printing,
            IReadOnlyList<OrderEntryViewModel> entries,
            bool isPreferredVersion,
            bool hasAnyPlaceholderForImage,
            decimal? totalCost,
            int totalQuantity,
            int checkedOutQuantity = 0,
            DateTime? checkedOutDate = null) => new()
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
            Entries = entries,
            HasAnyPlaceholderForImage = hasAnyPlaceholderForImage,
            IsPreferredVersion = isPreferredVersion,
            TotalCost = totalCost,
            TotalQuantity = totalQuantity
        };
    }
}
