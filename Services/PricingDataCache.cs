using CardCollector.DTO;
using CardCollector.Repository;

namespace CardCollector.Services
{
    public sealed class PricingDataCache : IPricingDataCache
    {
        private readonly ICardDataRepository _cardDataRepository;
        private readonly ITCGCatalogCache _tcgCatalogCache;
        private IReadOnlyDictionary<(string SetCode, string RarityName, string? PrintVariant), IReadOnlyList<TCGPriceSet>> _pricingIndex;

        public PricingDataCache(ICardDataRepository cardDataRepository, ITCGCatalogCache tcgCatalogCache)
        {
            _cardDataRepository = cardDataRepository;
            _tcgCatalogCache = tcgCatalogCache;
            _pricingIndex = BuildIndex(tcgCatalogCache.GetAllPrintings());
        }

        /// <summary>
        /// Groups printings by (SetCode, RarityName, PrintVariant) into a lookup index (public for direct unit
        /// testing). Cross-set-code collisions within the same key (e.g. old reprints sharing a set code across
        /// different tcgcsv groups) resolve by last-group-wins, since <see cref="ITCGCatalogCache"/> has no
        /// concept of which group "owns" a set code.
        /// </summary>
        public static IReadOnlyDictionary<(string SetCode, string RarityName, string? PrintVariant), IReadOnlyList<TCGPriceSet>> BuildIndex(
            IEnumerable<TCGPriceSet> printings)
        {
            var index = new Dictionary<(string, string, string?), List<TCGPriceSet>>();
            foreach (var group in printings.GroupBy(e => BuildKey(e.Code, e.RarityName, e.PrintVariant)))
                index[group.Key] = group.ToList();

            return index.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<TCGPriceSet>)kv.Value);
        }

        /// <summary>
        /// Looks up a card's known printings (via <see cref="ICardDataRepository"/>) in the pricing index, since
        /// tcgcsv.com has no concept of the card's numeric ID (public for direct unit testing).
        /// </summary>
        public static IReadOnlyList<TCGPriceSet> LookupCardSets(
            Card? card,
            IReadOnlyDictionary<(string SetCode, string RarityName, string? PrintVariant), IReadOnlyList<TCGPriceSet>> pricingIndex)
        {
            if (card?.CardSets is null)
                return [];

            var matches = new List<TCGPriceSet>();
            foreach (var set in card.CardSets)
            {
                if (string.IsNullOrWhiteSpace(set.Code))
                    continue;

                var key = BuildKey(set.Code, set.RarityName, set.PrintVariant);
                if (pricingIndex.TryGetValue(key, out var priceSets))
                    matches.AddRange(priceSets);
            }

            return matches;
        }

        public IReadOnlyList<TCGPriceSet> GetCardSets(int cardID) =>
            LookupCardSets(_cardDataRepository.GetCardByID(cardID), _pricingIndex);

        public async Task RefreshAsync()
        {
            await _tcgCatalogCache.RefreshAsync().ConfigureAwait(false);
            _pricingIndex = BuildIndex(_tcgCatalogCache.GetAllPrintings());
        }

        private static (string SetCode, string RarityName, string? PrintVariant) BuildKey(string setCode, string? rarityName, string? printVariant) =>
            (setCode.ToUpperInvariant(), (RarityExtensions.NormalizeRarityName(rarityName) ?? rarityName ?? string.Empty).ToUpperInvariant(), printVariant?.ToUpperInvariant());
    }
}
