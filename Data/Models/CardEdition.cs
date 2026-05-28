using System.ComponentModel.DataAnnotations;

namespace CardCollector.Data.Models
{
    public enum CardEdition
    {
        [Display(Name = "Unlimited")]
        Unlimited,

        [Display(Name = "1st Edition")]
        FirstEdition,

        [Display(Name = "Limited Edition")]
        LimitedEdition
    }
}
