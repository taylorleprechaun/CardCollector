using CardCollector.DTO;
using CardCollector.Repository;

namespace CardCollector.Services
{
    public sealed class PricingDataCache : IPricingDataCache
    {
        private readonly ICardDataRepository _cardDataRepository;
        private readonly ITCGCatalogCache _tcgCatalogCache;
        private volatile IReadOnlyDictionary<int, IReadOnlyList<TCGPriceSet>> _pricingIndex;

        public PricingDataCache(ICardDataRepository cardDataRepository, ITCGCatalogCache tcgCatalogCache)
        {
            _cardDataRepository = cardDataRepository;
            _tcgCatalogCache = tcgCatalogCache;
            _pricingIndex = BuildIndex(tcgCatalogCache.GetAllPrintings());
        }

        /// <summary>
        /// Groups price rows (one per product and edition) by TCGPlayer product id (public for direct unit testing). Rows
        /// without a product id, from a cache written before it was captured, are left out.
        /// </summary>
        public static IReadOnlyDictionary<int, IReadOnlyList<TCGPriceSet>> BuildIndex(IEnumerable<TCGPriceSet> printings) =>
            printings
                .Where(p => p.ProductID > 0)
                .GroupBy(p => p.ProductID)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<TCGPriceSet>)g.ToList());

        public IReadOnlyList<TCGPriceSet> GetCardSets(int cardID) =>
            LookupCardSets(_cardDataRepository.GetCardByID(cardID), _pricingIndex);

        /// <summary>
        /// Returns the price rows of every TCGPlayer product linked to the card's printings (public for direct unit testing).
        /// Each row is relabeled with its printing's set code, rarity, and variant, so callers match prices to printings by
        /// the catalog's values even when TCGPlayer words the product differently.
        /// </summary>
        public static IReadOnlyList<TCGPriceSet> LookupCardSets(Card? card, IReadOnlyDictionary<int, IReadOnlyList<TCGPriceSet>> pricingIndex)
        {
            if (card?.CardSets is null)
                return [];

            var matches = new List<TCGPriceSet>();
            foreach (var set in card.CardSets)
            {
                if (string.IsNullOrWhiteSpace(set.Code))
                    continue;

                foreach (var productID in set.TCGProductIDs)
                {
                    if (!pricingIndex.TryGetValue(productID, out var rows))
                        continue;

                    matches.AddRange(rows.Select(row => new TCGPriceSet
                    {
                        CardName = row.CardName,
                        Code = set.Code,
                        Edition = row.Edition,
                        GroupID = row.GroupID,
                        PriceRaw = row.PriceRaw,
                        PrintVariant = set.PrintVariant,
                        ProductID = row.ProductID,
                        RarityName = set.RarityName ?? row.RarityName
                    }));
                }
            }

            return matches;
        }

        public void RebuildIndex() => _pricingIndex = BuildIndex(_tcgCatalogCache.GetAllPrintings());
    }
}
