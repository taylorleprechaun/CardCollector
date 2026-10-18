using CardCollector.DTO;

namespace CardCollector.Models.Catalog
{
    /// <summary>
    /// The card data lookups inbox rows are built from: cards by id, passcode, and name, and printings by set code. The name
    /// and set code lookups return an empty list when nothing matches.
    /// </summary>
    public sealed class CatalogReviewLookup
    {
        public required Func<int, Card?> CardByID { get; init; }

        public required Func<int, Card?> CardByPasscode { get; init; }

        public required Func<string, IReadOnlyList<Card>> CardsByName { get; init; }

        public required Func<string, IReadOnlyList<CatalogPrintingRef>> PrintingsBySetCode { get; init; }
    }
}
