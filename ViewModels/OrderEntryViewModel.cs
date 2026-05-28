using CardCollector.Data.Models;

namespace CardCollector.ViewModels
{
    public class OrderEntryViewModel
    {
        public AcquisitionMethod? AcquisitionMethod { get; set; }

        public int CardID { get; set; }

        public string CardName { get; set; } = string.Empty;

        public CardCondition? Condition { get; set; }

        public DateTime DateCreated { get; set; }

        public CardEdition? Edition { get; set; }

        public int EntryID { get; set; }

        public int ImageID { get; set; }

        public string ImageURLSmall { get; set; } = string.Empty;

        public bool IsPlaceholder { get; set; }

        public decimal? PurchasePrice { get; set; }

        public DateTime? PurchaseDate { get; set; }

        public int Quantity { get; set; } = 1;

        public string RarityCode { get; set; } = string.Empty;

        public string SetCode { get; set; } = string.Empty;

        public string SetName { get; set; } = string.Empty;
    }
}
