using CardCollector.Data.Models;

namespace CardCollector.ViewModels
{
    public class CollectionGroupViewModel
    {
        private const int COMPLETE_THRESHOLD = 3;

        public int CardID { get; set; }

        public string CardName { get; set; } = string.Empty;

        public CollectionCompletionStatus CompletionStatus => NonPlaceholderQuantity switch
        {
            0 => CollectionCompletionStatus.Placeholder,
            >= COMPLETE_THRESHOLD => CollectionCompletionStatus.Complete,
            _ => CollectionCompletionStatus.Incomplete
        };

        public IList<OrderEntryViewModel> Entries { get; set; } = [];

        public int ImageID { get; set; }

        public string ImageURLSmall { get; set; } = string.Empty;

        public int NonPlaceholderQuantity => Entries.Where(e => !e.IsPlaceholder).Sum(e => e.Quantity);

        public string RarityCode { get; set; } = string.Empty;

        public string SetCode { get; set; } = string.Empty;

        public string SetName { get; set; } = string.Empty;

        public decimal? TotalCost { get; set; }

        public int TotalQuantity { get; set; }
    }
}
