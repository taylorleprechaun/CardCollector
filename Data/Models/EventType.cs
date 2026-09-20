using System.ComponentModel.DataAnnotations;

namespace CardCollector.Data.Models
{
    public enum EventType
    {
        Locals,

        NAWCQ,

        [Display(Name = "OTS Championship")]
        OTSChampionship,

        Other,

        Regional,

        [Display(Name = "Win-A-Mat")]
        WinAMat,

        YCS
    }
}
