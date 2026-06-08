using CardCollector.Data.Models;

namespace CardCollector.ViewModels
{
    public sealed class CollectionSearchCriteria
    {
        public AcquisitionMethod? AcquisitionMethod { get; set; }
        public string? CardType { get; set; }
        public CardCondition? Condition { get; set; }
        public CardEdition? Edition { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
        public string? Query { get; set; }
        public string? RarityName { get; set; }
        public string? SetName { get; set; }
    }
}
