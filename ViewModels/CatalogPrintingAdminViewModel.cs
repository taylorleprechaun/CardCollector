using CardCollector.Data.Models.Catalog;

namespace CardCollector.ViewModels
{
    /// <summary>One printing on the catalog admin card page, with its links and what it can be merged into.</summary>
    public sealed class CatalogPrintingAdminViewModel
    {
        /// <summary>"SET-EN001 Ultra Rare (Variant)".</summary>
        public string Description { get; init; } = string.Empty;

        public DateTime FirstSeenUTC { get; init; }

        public int ID { get; init; }

        /// <summary>True when the printing was added by hand rather than reported by a source.</summary>
        public bool IsManual { get; init; }

        public DateTime LastSeenUTC { get; init; }

        /// <summary>The card's other active printings this one can be merged into.</summary>
        public IReadOnlyList<CatalogReviewOption> MergeTargets { get; init; } = [];

        public string? PrintVariant { get; init; }

        public string RarityName { get; init; } = string.Empty;

        public string SetCode { get; init; } = string.Empty;

        public string? SetName { get; init; }

        public IReadOnlyList<CatalogPrintingSourceViewModel> Sources { get; init; } = [];

        public CatalogStatus Status { get; init; }

        /// <summary>Collection, wishlist, checked-out, and cart rows on this printing.</summary>
        public int UserRowCount { get; init; }
    }
}
