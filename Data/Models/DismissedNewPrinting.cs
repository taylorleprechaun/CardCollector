using System.ComponentModel.DataAnnotations;

namespace CardCollector.Data.Models
{
    public sealed class DismissedNewPrinting
    {
        [Required]
        public int CardID { get; set; }

        public DateTime DateCreated { get; set; }

        public DateTime DateModified { get; set; }

        public int ID { get; set; }

        [Required]
        public string RarityName { get; set; } = string.Empty;

        [Required]
        public string SetCode { get; set; } = string.Empty;
    }
}
