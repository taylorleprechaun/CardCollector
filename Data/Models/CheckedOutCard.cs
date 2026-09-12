using System.ComponentModel.DataAnnotations;

namespace CardCollector.Data.Models
{
    public class CheckedOutCard
    {
        [Required]
        public int CardID { get; set; }

        public DateTime CheckedOutDate { get; set; }

        public DateTime DateCreated { get; set; }

        public DateTime DateModified { get; set; }

        public int ID { get; set; }

        /// <summary>Null for a normal/base print; a distinct sellable variant of the same rarity otherwise (e.g. "Extended Art").</summary>
        public string? PrintVariant { get; set; }

        public int Quantity { get; set; } = 1;

        [Required]
        public string RarityName { get; set; } = string.Empty;

        [Required]
        public string SetCode { get; set; } = string.Empty;
    }
}
