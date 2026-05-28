using System.ComponentModel.DataAnnotations;

namespace CardCollector.Data.Models
{
    public enum AcquisitionMethod
    {
        [Display(Name = "Purchased")]
        Purchased,

        [Display(Name = "Traded")]
        Traded,

        [Display(Name = "Pulled from Pack")]
        Pulled
    }
}
