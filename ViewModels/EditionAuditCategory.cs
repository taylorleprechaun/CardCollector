using System.ComponentModel.DataAnnotations;

namespace CardCollector.ViewModels
{
    public enum EditionAuditCategory
    {
        // Recorded Edition isn't among the editions the API lists for this exact set/rarity.
        [Display(Name = "Edition Mismatch")]
        EditionMismatch,

        // No matching set/rarity printing was found in the live API data at all.
        [Display(Name = "Unverifiable")]
        Unverifiable
    }
}
