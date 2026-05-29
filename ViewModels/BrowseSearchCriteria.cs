namespace CardCollector.ViewModels
{
    public sealed class BrowseSearchCriteria
    {
        public string? Attribute { get; set; }
        public string? CardType { get; set; }
        public int? LevelMax { get; set; }
        public int? LevelMin { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
        public string? Query { get; set; }
        public string? RarityName { get; set; }
    }
}
