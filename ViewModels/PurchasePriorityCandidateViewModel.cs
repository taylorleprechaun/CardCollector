using CardCollector.Services;

namespace CardCollector.ViewModels
{
    public sealed class PurchasePriorityCandidateViewModel : CardPrinting
    {
        public string DebutDate { get; init; } = string.Empty;

        public int FoilCount { get; init; }

        public decimal LineTotal => (Price ?? 0) * QuantityNeeded;

        public string PrintingDate { get; init; } = string.Empty;

        public int QuantityNeeded => CompleteThreshold - QuantityOwned;

        public int QuantityOwned { get; init; } = 0;

        public static PurchasePriorityCandidateViewModel From(CardPrinting printing, PurchasePriorityCandidate candidate, int quantityOwned = 0) => new()
        {
            AvailableRarities = printing.AvailableRarities,
            CardID = printing.CardID,
            CardName = printing.CardName,
            CardType = printing.CardType,
            DebutDate = candidate.DebutDate,
            FoilCount = candidate.FoilCount,
            ImageID = printing.ImageID,
            ImageURLSmall = printing.ImageURLSmall,
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
