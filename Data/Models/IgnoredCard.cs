using System.ComponentModel.DataAnnotations;

namespace CardCollector.Data.Models
{
    public sealed class IgnoredCard
    {
        [Required]
        public int CatalogCardID { get; set; }

        public DateTime DateCreated { get; set; }

        public DateTime DateModified { get; set; }

        public int ID { get; set; }
    }
}
