namespace CardCollector.ViewModels
{
    /// <summary>A decision the inbox offers for a review item.</summary>
    public enum CatalogReviewAction
    {
        /// <summary>Apply what the item reports (a new passcode or name, retiring a card or printing).</summary>
        Accept,

        /// <summary>Create the missing printing on a chosen card, then link the product or restore the row on it.</summary>
        CreatePrinting,

        /// <summary>Acknowledge an informational item.</summary>
        Dismiss,

        /// <summary>Link the product, or restore the legacy row, on a chosen existing printing.</summary>
        MapToPrinting,

        /// <summary>Keep the catalog as it is (or discard the legacy row).</summary>
        Reject
    }
}
