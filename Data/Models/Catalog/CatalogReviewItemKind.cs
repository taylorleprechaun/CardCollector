using System.ComponentModel.DataAnnotations;

namespace CardCollector.Data.Models.Catalog
{
    /// <summary>
    /// What a review item reports. See <see cref="CatalogReviewItemKindExtensions"/> for which kinds need a decision and
    /// which sources must succeed before an open item can resolve itself.
    /// </summary>
    public enum CatalogReviewItemKind
    {
        [Display(Name = "Alias Added")]
        AliasAdded,

        [Display(Name = "Card Added")]
        CardAdded,

        [Display(Name = "Card Match Conflict")]
        CardMatchConflict,

        [Display(Name = "Card Name Changed")]
        CardNameChanged,

        [Display(Name = "Card Not Seen")]
        CardNotSeen,

        [Display(Name = "Card Text Changed")]
        CardTextChanged,

        /// <summary>A legacy "dismissed new printing" flag for a printing the catalog doesn't have; there is nothing left to dismiss.</summary>
        [Display(Name = "Legacy Dismissal Unmapped")]
        LegacyDismissalUnmapped,

        [Display(Name = "Legacy Row Auto-Mapped")]
        LegacyRowAutoMapped,

        [Display(Name = "Legacy Row Unmapped")]
        LegacyRowUnmapped,

        [Display(Name = "Passcode Assigned")]
        PasscodeAssigned,

        [Display(Name = "Passcode Changed")]
        PasscodeChanged,

        [Display(Name = "Printing Added")]
        PrintingAdded,

        [Display(Name = "Printing Not Seen")]
        PrintingNotSeen,

        [Display(Name = "Sync Anomaly")]
        SyncAnomaly,

        [Display(Name = "TCGPlayer Listing Changed")]
        TCGListingChanged,

        [Display(Name = "TCGPlayer Product Changed")]
        TCGProductChanged,

        [Display(Name = "Unmapped TCGPlayer Product")]
        UnmappedTCGProduct
    }
}
