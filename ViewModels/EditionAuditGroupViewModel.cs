using CardCollector.Data.Models;

namespace CardCollector.ViewModels
{
    public sealed class EditionAuditGroupViewModel : CardPrinting
    {
        public IReadOnlyList<EditionAuditEntryViewModel> Entries { get; init; } = [];

        public EditionAuditCategory FlaggedCategory { get; init; }

        public int FlaggedCount { get; init; }

        public static EditionAuditGroupViewModel From(
            CardPrinting printing,
            IReadOnlyList<EditionAuditEntryViewModel> entries) => new()
        {
            AvailableRarities = printing.AvailableRarities,
            CardID = printing.CardID,
            CardName = printing.CardName,
            CardType = printing.CardType,
            Entries = entries,
            FlaggedCategory = entries.Where(e => e.Category.HasValue).Min(e => e.Category!.Value),
            FlaggedCount = entries.Count(e => e.Category.HasValue),
            ImageID = printing.ImageID,
            ImageURLSmall = printing.ImageURLSmall,
            Price = printing.Price,
            RarityCode = printing.RarityCode,
            RarityName = printing.RarityName,
            SetCode = printing.SetCode,
            SetName = printing.SetName
        };
    }
}
