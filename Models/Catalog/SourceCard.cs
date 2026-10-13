using CardCollector.Data.Models.Catalog;
using CardCollector.DTO;

namespace CardCollector.Models.Catalog
{
    /// <summary>A card as one source (yaml-yugi or YGOProDeck) reports it, reduced to what the catalog keeps.</summary>
    public sealed class SourceCard
    {
        public string? Archetype { get; init; }

        /// <summary>YGOProDeck artwork ids other than <see cref="YGOProDeckID"/>.</summary>
        public IReadOnlyList<int> ArtworkIDs { get; init; } = [];

        public int? ATK { get; init; }

        public string? Attribute { get; init; }

        public int? BetaID { get; init; }

        public string? CardType { get; init; }

        public int? DEF { get; init; }

        public string? Description { get; init; }

        public int? FakePassword { get; init; }

        public IReadOnlyList<Image> Images { get; init; } = [];

        public int? KonamiID { get; init; }

        public int? Level { get; init; }

        public string? LinkMarkers { get; init; }

        public int? LinkRating { get; init; }

        public string Name { get; init; } = string.Empty;

        /// <summary>yaml-yugi's passcode. Null for YGOProDeck, whose id goes in <see cref="YGOProDeckID"/>.</summary>
        public int? Passcode { get; init; }

        public IReadOnlyList<SourcePrinting> Printings { get; init; } = [];

        public CatalogSource Source { get; init; }

        public string? Type { get; init; }

        public string? TypeLine { get; init; }

        public int? YGOProDeckID { get; init; }
    }
}
