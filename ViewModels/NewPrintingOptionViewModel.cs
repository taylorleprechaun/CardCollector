namespace CardCollector.ViewModels
{
    public sealed class NewPrintingOptionViewModel
    {
        /// <summary>The catalog printing this option offers.</summary>
        public int PrintingID { get; set; }

        public string? PrintVariant { get; set; }

        public string RarityName { get; set; } = string.Empty;

        public string? ReleaseDate { get; set; }

        public string SetCode { get; set; } = string.Empty;

        public string SetName { get; set; } = string.Empty;
    }
}
