using CardCollector.Data.Models.Catalog;

namespace CardCollector.Models.Catalog
{
    /// <summary>Which catalog card each observed card belongs to.</summary>
    public sealed class CatalogCardMatchResult
    {
        /// <summary>Observed cards whose only candidate catalog card was already claimed by another observed card this run.</summary>
        public IReadOnlyList<(ObservedCard Observed, CatalogCard ClaimedCard)> Conflicts { get; init; } = [];

        /// <summary>Every other observed card, with its catalog card, or null when it is new to the catalog.</summary>
        public IReadOnlyList<(ObservedCard Observed, CatalogCard? Card)> Matches { get; init; } = [];
    }
}
