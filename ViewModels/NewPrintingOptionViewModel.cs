namespace CardCollector.ViewModels
{
    public sealed class NewPrintingOptionViewModel
    {
        public string RarityName { get; set; } = string.Empty;

        public string? ReleaseDate { get; set; }

        public string SetCode { get; set; } = string.Empty;

        public string SetName { get; set; } = string.Empty;
    }
}
