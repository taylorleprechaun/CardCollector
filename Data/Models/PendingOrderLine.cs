using System.ComponentModel.DataAnnotations;

namespace CardCollector.Data.Models
{
    public class PendingOrderLine
    {
        public AcquisitionMethod? AcquisitionMethod { get; set; }

        [Required]
        public int CardID { get; set; }

        public CardCondition? Condition { get; set; }

        public DateTime DateCreated { get; set; }

        public CardEdition? Edition { get; set; }

        public int ID { get; set; }

        [Required]
        public int ImageID { get; set; }

        public decimal? MarketPriceAtEntry { get; set; }

        public DateTime? PurchaseDate { get; set; }

        public decimal? PurchasePrice { get; set; }

        public int Quantity { get; set; } = 1;

        public string? RarityName { get; set; }

        [Required]
        public string SetCode { get; set; } = string.Empty;
    }
}
