using System.ComponentModel.DataAnnotations;

namespace CardCollector.Data.Models
{
    public enum CardCondition
    {
        [Display(Name = "Damaged")]
        Damaged,

        [Display(Name = "Heavily Played")]
        HeavilyPlayed,

        [Display(Name = "Lightly Played")]
        LightlyPlayed,

        [Display(Name = "Moderately Played")]
        ModeratelyPlayed,

        [Display(Name = "Near Mint")]
        NearMint
    }
}
