using System.ComponentModel.DataAnnotations;

namespace CardCollector.Data.Models
{
    public class CollectionEntry
    {
        public int ID { get; set; }

        [Required]
        public int CardID { get; set; }

        [Required]
        public int ImageID { get; set; }

        [Required]
        public string SetCode { get; set; } = string.Empty;

        [Required]
        public CollectionStatus Status { get; set; }

        public AcquisitionMethod? AcquisitionMethod { get; set; }

        public CardCondition? Condition { get; set; }

        public CardEdition? Edition { get; set; }

        public bool IsPlaceholder { get; set; }

        public DateTime? PurchaseDate { get; set; }

        public int Quantity { get; set; } = 1;

        public decimal? PurchasePrice { get; set; }

        public DateTime DateCreated { get; set; }

        public DateTime DateModified { get; set; }
    }
}
