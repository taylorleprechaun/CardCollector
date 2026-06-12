using CardCollector.Data.Models;

namespace CardCollector.ViewModels
{
    public sealed class CollectionSearchCriteria : SearchCriteria
    {
        public AcquisitionMethod? AcquisitionMethod { get; set; }
        public CardCondition? Condition { get; set; }
        public CardEdition? Edition { get; set; }
        public bool? IsCheckedOut { get; set; }
    }
}
