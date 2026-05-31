namespace CardCollector.ViewModels
{
    public sealed class CollectionEntryModalViewModel
    {
        public int CardID { get; set; }
        public int ImageID { get; set; }
        public int? PageID { get; set; }
        public string PageURL { get; set; } = string.Empty;
        public string? ReturnURL { get; set; }
    }
}
