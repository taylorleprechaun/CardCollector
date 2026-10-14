using System.ComponentModel.DataAnnotations;

namespace CardCollector.Data.Models
{
    public class PreferredVersion : IPrintingKeyedRow
    {
        [Required]
        public int CardID { get; set; }

        /// <summary>The catalog printing this row refers to. Set by the legacy migration; the app switches to it from the set code, rarity, and variant columns in a later phase.</summary>
        public int? CatalogPrintingID { get; set; }

        public DateTime DateCreated { get; set; }

        public DateTime DateModified { get; set; }

        public int DesiredQuantity { get; set; } = 3;

        public int ID { get; set; }

        /// <summary>Null for a normal/base print; a distinct sellable variant of the same rarity otherwise (e.g. "Extended Art").</summary>
        public string? PrintVariant { get; set; }

        public string? RarityName { get; set; }

        [Required]
        public string SetCode { get; set; } = string.Empty;
    }
}
