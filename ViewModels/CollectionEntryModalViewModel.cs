namespace CardCollector.ViewModels
{
    public class CollectionEntryModalViewModel
    {
        public int CardID { get; set; }
        public int ImageID { get; set; }
        public int? PageID { get; set; }
        public string PageUrl { get; set; } = string.Empty;
        public string? ReturnUrl { get; set; }
    }
}
