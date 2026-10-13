using CardCollector.DTO;

namespace CardCollector.Models.Catalog
{
    /// <summary>
    /// The catalog as the rest of the app reads it: every card in the <see cref="Card"/>/<see cref="Set"/> shape, plus the
    /// lookups built from them. Immutable, so a reload swaps in a whole new instance.
    /// </summary>
    public sealed class CatalogReadModel
    {
        public static CatalogReadModel Empty { get; } = new();

        /// <summary>Cards with at least one active printing; the ones Browse and the collection views list.</summary>
        public IReadOnlyList<Card> BrowseableCards { get; init; } = [];

        public IReadOnlyList<Card> Cards { get; init; } = [];

        public IReadOnlyDictionary<int, Card> CardsByID { get; init; } = new Dictionary<int, Card>();

        public IReadOnlyList<string> DistinctRarityNames { get; init; } = [];

        public IReadOnlyList<string> DistinctSetNames { get; init; } = [];

        /// <summary>Every known passcode or source id, mapped to the catalog card it belongs to.</summary>
        public IReadOnlyDictionary<int, int> PasscodeAliases { get; init; } = new Dictionary<int, int>();

        public IReadOnlyDictionary<string, string> SetNamesByCode { get; init; } = new Dictionary<string, string>();

        public IReadOnlyDictionary<string, string> SetPrefixByName { get; init; } = new Dictionary<string, string>();
    }
}
