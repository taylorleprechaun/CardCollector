namespace CardCollector.ViewModels
{
    /// <summary>A decision the inbox offers for a review item.</summary>
    public enum CatalogReviewAction
    {
        /// <summary>Apply what the item reports (a new passcode or name, retiring a card or printing).</summary>
        Accept,

        /// <summary>Create the card an unmapped product names when the catalog has no card by that name, with the printing, and link the product.</summary>
        CreateCard,

        /// <summary>Create the missing printing on a chosen card, then link the product or restore the row on it.</summary>
        CreatePrinting,

        /// <summary>Acknowledge an informational item.</summary>
        Dismiss,

        /// <summary>Link the product to the retired printing it matches, leaving that printing retired.</summary>
        LinkKeepRetired,

        /// <summary>Link the product to a chosen existing printing.</summary>
        MapToPrinting,

        /// <summary>Keep the catalog as it is.</summary>
        Reject,

        /// <summary>Put a rejected or dismissed item back in front of the user, undecided.</summary>
        Reopen
    }
}
