namespace CardCollector.ViewModels
{
    public sealed class BrowseSearchCriteria : SearchCriteria
    {
        public bool? InCollection { get; set; }
        public bool? InWishlist { get; set; }
        public bool? IsOrdered { get; set; }
    }
}
