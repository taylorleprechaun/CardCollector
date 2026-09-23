using System.ComponentModel.DataAnnotations;

namespace CardCollector.Models
{
    /// <summary>A card's Forbidden &amp; Limited status. The numeric value is also the copies allowed.</summary>
    public enum BanlistLimit
    {
        Forbidden = 0,
        Limited = 1,

        [Display(Name = "Semi-Limited")]
        SemiLimited = 2,
        Unlimited = 3
    }
}
