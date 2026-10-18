using CardCollector.Data.Models.Catalog;

namespace CardCollector.Models.Catalog
{
    /// <summary>
    /// A review item a sync run found, before it is reconciled with the stored items. Holds the card or printing itself
    /// rather than its id, because a run can report on rows it created that have no id until they are saved.
    /// </summary>
    public sealed class DetectedReviewItem
    {
        public CatalogCard? Card { get; init; }

        /// <summary>
        /// The subject when the item is about something outside the catalog (e.g. "tcg:687323"). Takes precedence over
        /// <see cref="Printing"/> and <see cref="Card"/>, which then only link the item to the catalog row it concerns.
        /// </summary>
        public string? ExternalSubject { get; init; }

        public CatalogReviewItemKind Kind { get; init; }

        /// <summary>The catalog card to link the item to when it holds no <see cref="Card"/> or <see cref="Printing"/> (a legacy row matched by ID).</summary>
        public int? LinkedCardID { get; init; }

        /// <summary>The catalog printing to link the item to when it holds no <see cref="Printing"/>, like <see cref="LinkedCardID"/>.</summary>
        public int? LinkedPrintingID { get; init; }

        /// <summary>Serialized to <see cref="CatalogReviewItem.PayloadJSON"/>; keep property order stable so its hash is too.</summary>
        public object Payload { get; init; } = new { };

        public CatalogPrinting? Printing { get; init; }

        /// <summary>The <see cref="CatalogReviewItem.SubjectKey"/>. Read it only after the run's new rows are saved.</summary>
        public string SubjectKey =>
            ExternalSubject
            ?? (Printing is not null ? $"printing:{Printing.ID}" : null)
            ?? (Card is not null ? $"card:{Card.ID}" : null)
            ?? throw new InvalidOperationException("A detected review item needs a card, a printing, or an external subject.");

        public string Summary { get; init; } = string.Empty;
    }
}
