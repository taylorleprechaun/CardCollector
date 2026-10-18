using CardCollector.Data.Models.Catalog;
using System.ComponentModel.DataAnnotations;

namespace CardCollector.Data.Models
{
    public class CollectionEntry
    {
        public AcquisitionMethod? AcquisitionMethod { get; set; }

        /// <summary>Lets queries reach the printing (and its card); not loaded unless a query includes it.</summary>
        public CatalogPrinting? CatalogPrinting { get; set; }

        /// <summary>The catalog printing this row refers to.</summary>
        public int CatalogPrintingID { get; set; }

        public CardCondition? Condition { get; set; }

        public DateTime DateCreated { get; set; }

        public DateTime DateModified { get; set; }

        public CardEdition? Edition { get; set; }

        public int ID { get; set; }

        public decimal? MarketPriceAtEntry { get; set; }

        public DateTime? PurchaseDate { get; set; }

        public decimal? PurchasePrice { get; set; }

        public int Quantity { get; set; } = 1;

        [Required]
        public CollectionStatus Status { get; set; }
    }
}
