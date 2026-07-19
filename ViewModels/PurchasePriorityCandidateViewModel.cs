using CardCollector.Services;

namespace CardCollector.ViewModels
{
    public sealed class PurchasePriorityCandidateViewModel : CardPrinting
    {
        public int CartQuantity { get; init; }

        public string DebutDate { get; init; } = string.Empty;

        public int FoilCount { get; init; }

        public bool HasAmbiguousSetCode { get; init; }

        public bool IsInCart => CartQuantity > 0;

        public bool IsOrdered => OrderedQuantity > 0;

        public decimal LineTotal => (Price ?? 0) * QuantityNeeded;

        public int OrderedQuantity { get; init; }

        public string PrintingDate { get; init; } = string.Empty;

        public int QuantityNeeded => Math.Max(0, CompleteThreshold - QuantityOwned - OrderedQuantity - CartQuantity);

        public int QuantityOwned { get; init; } = 0;

        public static PurchasePriorityCandidateViewModel From(CardPrinting printing, PurchasePriorityCandidate candidate, int quantityOwned = 0, bool hasAmbiguousSetCode = false, int cartQuantity = 0, int orderedQuantity = 0) => new()
        {
            AvailableRarities = printing.AvailableRarities,
            CardID = printing.CardID,
            CardName = printing.CardName,
            CardType = printing.CardType,
            CartQuantity = cartQuantity,
            DebutDate = candidate.DebutDate,
            FoilCount = candidate.FoilCount,
            HasAmbiguousSetCode = hasAmbiguousSetCode,
            ImageID = printing.ImageID,
            ImageURLSmall = printing.ImageURLSmall,
            OrderedQuantity = orderedQuantity,
            Price = printing.Price,
            PrintingDate = candidate.PrintingDate,
            QuantityOwned = quantityOwned,
            RarityCode = printing.RarityCode,
            RarityName = printing.RarityName,
            SetCode = printing.SetCode,
            SetName = printing.SetName
        };
    }
}
