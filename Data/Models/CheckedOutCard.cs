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

        [Required]
        public int ImageID { get; set; }

        public int Quantity { get; set; } = 1;

        [Required]
        public string SetCode { get; set; } = string.Empty;
    }
}
