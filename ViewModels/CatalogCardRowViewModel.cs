using CardCollector.Data.Models.Catalog;

namespace CardCollector.ViewModels
{
    /// <summary>One card in the catalog admin search results.</summary>
    public sealed class CatalogCardRowViewModel
    {
        /// <summary>True when some field of the card was edited by hand and is locked against the sync.</summary>
        public bool HasLockedFields { get; init; }

        public int ID { get; init; }

        public string? ImageURLSmall { get; init; }

        public int? KonamiID { get; init; }

        public string Name { get; init; } = string.Empty;

        public int? Passcode { get; init; }

        /// <summary>Active printings only.</summary>
        public int PrintingCount { get; init; }

        public CatalogStatus Status { get; init; }
    }
}
