using CardCollector.Data.Models;
using CardCollector.DTO;

namespace CardCollector.Services
{
    public sealed class PricingService : IPricingService
    {
        private readonly IPricingDataCache _pricingDataCache;

        public PricingService(IPricingDataCache pricingDataCache)
        {
            _pricingDataCache = pricingDataCache;
        }

        public Task<IReadOnlyDictionary<(string SetCode, string RarityName), IReadOnlySet<CardEdition>>> GetCardEditionMapAsync(int cardID)
        {
            var cardSets = _pricingDataCache.GetCardSets(cardID);

            var map = cardSets
                .GroupBy(s => (SetCode: s.Code.ToUpperInvariant(), RarityName: s.RarityName.ToUpperInvariant()))
                .ToDictionary(
                    g => g.Key,
                    g => (IReadOnlySet<CardEdition>)g
                        .Select(s => CardEditionExtensions.TryParseTCGAPIEditionName(s.Edition, out var e) ? (CardEdition?)e : null)
                        .Where(e => e.HasValue)
                        .Select(e => e!.Value)
                        .ToHashSet());

            return Task.FromResult<IReadOnlyDictionary<(string, string), IReadOnlySet<CardEdition>>>(map);
        }

        public Task<decimal?> GetPrintingPriceAsync(int cardID, string setCode, string rarityName, CardEdition? edition = null)
        {
            var cardSets = _pricingDataCache.GetCardSets(cardID);
            var match = FindMatch(cardSets, setCode, rarityName, edition);

            return Task.FromResult(match is null || match.Price == 0m ? (decimal?)null : match.Price);
        }

        private static TCGPriceSet? FindMatch(IEnumerable<TCGPriceSet> cardSets, string setCode, string rarityName, CardEdition? edition)
        {
            bool IsSetRarityMatch(TCGPriceSet s) =>
                string.Equals(s.Code, setCode, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(s.RarityName, rarityName, StringComparison.OrdinalIgnoreCase);

            if (edition is not null)
            {
                var editionMatch = cardSets.FirstOrDefault(s =>
                    IsSetRarityMatch(s) &&
                    string.Equals(s.Edition, edition.Value.GetTCGAPIEditionName(), StringComparison.OrdinalIgnoreCase));

                if (editionMatch is not null)
                    return editionMatch;
            }

            // No exact edition match — fall back to set/rarity only (older sets may not carry set_edition).
            return cardSets.FirstOrDefault(IsSetRarityMatch);
        }
    }
}
