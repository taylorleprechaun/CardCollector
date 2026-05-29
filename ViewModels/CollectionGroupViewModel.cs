using CardCollector.Data.Models;

namespace CardCollector.ViewModels
{
    public class CollectionGroupViewModel
    {
        private const int COMPLETE_THRESHOLD = 3;

        public int CardID { get; set; }

        public string CardName { get; set; } = string.Empty;

        public CollectionCompletionStatus CompletionStatus =>
            !IsPreferredVersion
                ? CollectionCompletionStatus.Placeholder
                : TotalQuantity >= COMPLETE_THRESHOLD
                    ? CollectionCompletionStatus.Complete
                    : CollectionCompletionStatus.Incomplete;

        public IList<OrderEntryViewModel> Entries { get; set; } = [];

        public int ImageID { get; set; }

        public string ImageURLSmall { get; set; } = string.Empty;

        public bool IsPreferredVersion { get; set; }

        public string RarityCode { get; set; } = string.Empty;

        public string SetCode { get; set; } = string.Empty;

        public string SetName { get; set; } = string.Empty;

        public decimal? TotalCost { get; set; }

        public int TotalQuantity { get; set; }
    }
}
