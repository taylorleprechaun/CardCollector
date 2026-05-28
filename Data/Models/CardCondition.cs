using System.ComponentModel.DataAnnotations;

namespace CardCollector.Data.Models
{
    public enum CardCondition
    {
        [Display(Name = "Near Mint")]
        NearMint,

        [Display(Name = "Lightly Played")]
        LightlyPlayed,

        [Display(Name = "Moderately Played")]
        ModeratelyPlayed,

        [Display(Name = "Heavily Played")]
        HeavilyPlayed,

        [Display(Name = "Damaged")]
        Damaged
    }
}
