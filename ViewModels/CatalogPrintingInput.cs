namespace CardCollector.ViewModels
{
    /// <summary>A printing added to a catalog card by hand.</summary>
    public sealed class CatalogPrintingInput
    {
        /// <summary>TCGPlayer's qualifier (e.g. "Extended Art"); empty for the plain print.</summary>
        public string? PrintVariant { get; set; }

        public string? RarityName { get; set; }

        public string? SetCode { get; set; }
    }
}
