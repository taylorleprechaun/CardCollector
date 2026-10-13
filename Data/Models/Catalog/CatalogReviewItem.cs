namespace CardCollector.Data.Models.Catalog
{
    /// <summary>
    /// One thing the catalog sync wants the user to know about or decide. Items accumulate instead of repeating: the same
    /// (<see cref="Kind"/>, <see cref="SubjectKey"/>) found again only updates <see cref="LastDetectedUTC"/> and
    /// <see cref="DetectionCount"/>.
    /// </summary>
    public sealed class CatalogReviewItem
    {
        /// <summary>The card the item is about, when there is one. Not a foreign key, so items outlive any catalog edits.</summary>
        public int? CatalogCardID { get; set; }

        /// <summary>The printing the item is about, when there is one. Not a foreign key, like <see cref="CatalogCardID"/>.</summary>
        public int? CatalogPrintingID { get; set; }

        public DateTime DateCreated { get; set; }

        public DateTime DateModified { get; set; }

        public int DetectionCount { get; set; }

        public DateTime FirstDetectedUTC { get; set; }

        public int ID { get; set; }

        public CatalogReviewItemKind Kind { get; set; }

        public DateTime LastDetectedUTC { get; set; }

        public int? LastSyncRunID { get; set; }

        /// <summary>Hash of <see cref="PayloadJson"/>; a closed item reopens only when a new detection's hash differs.</summary>
        public string PayloadHash { get; set; } = string.Empty;

        /// <summary>JSON describing the item (typically the current and reported values).</summary>
        public string PayloadJson { get; set; } = "{}";

        /// <summary>What was done about the item, in words.</summary>
        public string? Resolution { get; set; }

        public DateTime? ResolvedUTC { get; set; }

        public CatalogReviewItemStatus Status { get; set; }

        /// <summary>Identifies what the item is about within its kind (e.g. "card:42", "tcg:687323").</summary>
        public string SubjectKey { get; set; } = string.Empty;

        public string Summary { get; set; } = string.Empty;
    }
}
