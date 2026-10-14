using CardCollector.DTO;

namespace CardCollector.Models.Catalog
{
    /// <summary>The card data indexed for building inbox rows: printings by set code, cards by name, id, and passcode.</summary>
    public sealed class CatalogReviewLookup
    {
        public required Func<int, Card?> CardByID { get; init; }

        public required Func<int, Card?> CardByPasscode { get; init; }

        public IReadOnlyDictionary<string, IReadOnlyList<Card>> CardsByName { get; init; } =
            new Dictionary<string, IReadOnlyList<Card>>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, IReadOnlyList<(Card Card, Set Printing)>> PrintingsBySetCode { get; init; } =
            new Dictionary<string, IReadOnlyList<(Card, Set)>>(StringComparer.OrdinalIgnoreCase);
    }
}
