using CardCollector.Data.Models.Catalog;

namespace CardCollector.ViewModels
{
    /// <summary>A review item as the inbox shows it, with the decisions it allows.</summary>
    public sealed class CatalogReviewItemViewModel
    {
        /// <summary>Wording for the Accept button, e.g. "Use passcode 74733322".</summary>
        public string? AcceptLabel { get; init; }

        public IReadOnlyList<CatalogReviewAction> Actions { get; init; } = [];

        public string? CardName { get; init; }
        /// <summary>Cards a missing printing can be created on.</summary>
        public IReadOnlyList<CatalogReviewOption> CardOptions { get; init; } = [];

        public int? CatalogCardID { get; init; }

        /// <summary>The item's payload as label/value lines, for showing what was detected.</summary>
        public IReadOnlyList<KeyValuePair<string, string>> Details { get; init; } = [];

        public int DetectionCount { get; init; }

        /// <summary>
        /// The card the row shows art for and links to: the item's own card, else the one catalog card with the name the item
        /// reports (an unmapped product names its card but isn't attached to one). Null when neither is known.
        /// </summary>
        public int? DisplayCardID { get; init; }

        /// <summary>Why the last decision on this item failed, shown on its row; null when it didn't.</summary>
        public string? ErrorMessage { get; init; }

        public DateTime FirstDetectedUTC { get; init; }

        /// <summary>A short line saying what was detected, e.g. "No catalog printing has this set code"; the summary when the kind has none.</summary>
        public string Headline { get; init; } = string.Empty;
        public int ID { get; init; }

        public string? ImageURLSmall { get; init; }

        public bool IsActionable { get; init; }

        public CatalogReviewItemKind Kind { get; init; }

        public DateTime LastDetectedUTC { get; init; }

        /// <summary>The set code an unmapped product reports; null for other items or when it reports none.</summary>
        public string? PlacementSetCode { get; init; }

        /// <summary>True when an active printing has <see cref="PlacementSetCode"/>, so the map options are all under it.</summary>
        public bool PlacementSetCodeInCatalog { get; init; }

        /// <summary>The decision the row leads with; the others sit behind "Other ways to resolve". Null when nothing is allowed.</summary>
        public CatalogReviewAction? PrimaryAction { get; init; }

        /// <summary>Existing printings the item can be mapped to.</summary>
        public IReadOnlyList<CatalogReviewOption> PrintingOptions { get; init; } = [];

        public string? PrintVariant { get; init; }

        /// <summary>The TCGPlayer product the item reports; null when it reports none.</summary>
        public int? ProductID { get; init; }

        public string? RarityName { get; init; }

        /// <summary>Wording for the Reject button, e.g. "Keep 48739627".</summary>
        public string? RejectLabel { get; init; }

        public string? Resolution { get; init; }

        public DateTime? ResolvedUTC { get; init; }

        /// <summary>The set code the item reports; null when it reports none.</summary>
        public string? SetCode { get; init; }

        public CatalogReviewItemStatus Status { get; init; }

        public string Summary { get; init; } = string.Empty;
    }
}
