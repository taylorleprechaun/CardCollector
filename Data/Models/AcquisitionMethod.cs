using System.ComponentModel.DataAnnotations;

namespace CardCollector.Data.Models
{
    public enum AcquisitionMethod
    {
        [Display(Name = "Pulled from Pack")]
        Pulled,

        [Display(Name = "Purchased")]
        Purchased,

        [Display(Name = "Traded")]
        Traded
    }
}
