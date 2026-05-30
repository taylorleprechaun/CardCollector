namespace CardCollector.ViewModels
{
    public class CollectionFormFieldsViewModel
    {
        public string Prefix { get; set; } = "field";
        public bool AcquisitionVisible { get; set; }
        public bool MarketPriceEditable { get; set; } = true;
        public bool ShowPreferred { get; set; }
    }
}
