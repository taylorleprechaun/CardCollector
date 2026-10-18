using System.ComponentModel.DataAnnotations;

namespace CardCollector.ViewModels
{
    /// <summary>Which review items the inbox shows.</summary>
    public enum CatalogReviewFilter
    {
        /// <summary>Open informational items: changes the sync already applied.</summary>
        Informational,

        /// <summary>Open items waiting on a decision.</summary>
        [Display(Name = "Needs Decision")]
        NeedsDecision,

        /// <summary>Items that are no longer open, however they were closed.</summary>
        Resolved
    }
}
