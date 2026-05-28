using CardCollector.Data.Models;

namespace CardCollector.ViewModels
{
    public class CollectionGroupViewModel
    {
        public int CardID { get; set; }

        public string CardName { get; set; } = string.Empty;

        public List<OrderEntryViewModel> Entries { get; set; } = [];

        public int ImageID { get; set; }

        public string ImageURLSmall { get; set; } = string.Empty;

        public string RarityCode { get; set; } = string.Empty;

        public string SetCode { get; set; } = string.Empty;

        public string SetName { get; set; } = string.Empty;

        public decimal? TotalCost { get; set; }

        public int TotalQuantity { get; set; }

        public int NonPlaceholderQuantity => Entries.Where(e => !e.IsPlaceholder).Sum(e => e.Quantity);

        public CollectionCompletionStatus CompletionStatus => NonPlaceholderQuantity switch
        {
            0    => CollectionCompletionStatus.Placeholder,
            >= 3 => CollectionCompletionStatus.Complete,
            _    => CollectionCompletionStatus.Incomplete
        };
    }
}
