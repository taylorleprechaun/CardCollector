namespace CardCollector.ViewModels
{
    public sealed class CollectionFormFieldsViewModel
    {
        public bool AcquisitionVisible { get; set; }
        public bool MarketPriceEditable { get; set; } = true;
        public string Prefix { get; set; } = "field";
        public bool ShowPreferred { get; set; }
    }
}
