namespace CardCollector.ViewModels
{
    public sealed class NewPrintingOpportunityViewModel
    {
        public int CardID { get; set; }

        public string CardName { get; set; } = string.Empty;

        public string CurrentRarityName { get; set; } = string.Empty;

        public string? CurrentReleaseDate { get; set; }

        public string CurrentSetCode { get; set; } = string.Empty;

        public string CurrentSetName { get; set; } = string.Empty;

        public int ImageID { get; set; }

        public string ImageURLSmall { get; set; } = string.Empty;

        public bool IsIgnored { get; set; }

        public IReadOnlyList<NewPrintingOptionViewModel> NewerPrintings { get; set; } = [];
    }
}
