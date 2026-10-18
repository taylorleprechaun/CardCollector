using CardCollector.Data.Models.Catalog;

namespace CardCollector.ViewModels
{
    /// <summary>Everything the catalog admin card page shows and edits for one card.</summary>
    public sealed class CatalogCardDetailsViewModel
    {
        public IReadOnlyList<CatalogCardAliasViewModel> Aliases { get; init; } = [];

        /// <summary>The card's current values, used to fill the edit form.</summary>
        public CatalogCardEditInput Fields { get; init; } = new();

        public DateTime FirstSeenUTC { get; init; }

        public int ID { get; init; }

        public string? ImageURLSmall { get; init; }

        public int? KonamiID { get; init; }

        public DateTime LastSeenUTC { get; init; }

        /// <summary>Names of the fields edited by hand (see <see cref="CatalogCardField"/>), which the sync leaves alone.</summary>
        public IReadOnlyList<string> LockedFields { get; init; } = [];

        public string Name { get; init; } = string.Empty;

        /// <summary>Active printings first, then retired ones, each ordered by set code.</summary>
        public IReadOnlyList<CatalogPrintingAdminViewModel> Printings { get; init; } = [];

        /// <summary>Rarity names already in the catalog, offered as suggestions when adding a printing.</summary>
        public IReadOnlyList<string> RarityNames { get; init; } = [];

        public CatalogStatus Status { get; init; }

        /// <summary>Collection, wishlist, checked-out, and cart rows on any printing of the card.</summary>
        public int UserRowCount { get; init; }

        public bool IsLocked(string fieldName) => LockedFields.Contains(fieldName, StringComparer.OrdinalIgnoreCase);
    }
}
