using System.ComponentModel.DataAnnotations;

namespace CardCollector.Data.Models
{
    public class CollectionEntry
    {
        public AcquisitionMethod? AcquisitionMethod { get; set; }

        [Required]
        public int CardID { get; set; }

        public CardCondition? Condition { get; set; }

        public DateTime DateCreated { get; set; }

        public DateTime DateModified { get; set; }

        public CardEdition? Edition { get; set; }

        public int ID { get; set; }

        [Required]
        public int ImageID { get; set; }

        public bool IsPlaceholder { get; set; }

        public DateTime? PurchaseDate { get; set; }

        public decimal? PurchasePrice { get; set; }

        public int Quantity { get; set; } = 1;

        [Required]
        public string SetCode { get; set; } = string.Empty;

        [Required]
        public CollectionStatus Status { get; set; }
    }
}
