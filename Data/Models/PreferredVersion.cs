using System.ComponentModel.DataAnnotations;

namespace CardCollector.Data.Models
{
    public class PreferredVersion
    {
        [Required]
        public int CardID { get; set; }

        public DateTime DateCreated { get; set; }

        public DateTime DateModified { get; set; }

        public int ID { get; set; }

        [Required]
        public int ImageID { get; set; }

        public string? RarityName { get; set; }

        [Required]
        public string SetCode { get; set; } = string.Empty;
    }
}
