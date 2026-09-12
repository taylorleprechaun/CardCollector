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

        public Task<IReadOnlyDictionary<(string SetCode, string RarityName, string? PrintVariant), IReadOnlySet<CardEdition>>> GetCardEditionMapAsync(int cardID)
        {
            var cardSets = _pricingDataCache.GetCardSets(cardID);

            var map = cardSets
                .GroupBy(s => (
                    SetCode: s.Code.ToUpperInvariant(),
                    RarityName: (RarityExtensions.NormalizeRarityName(s.RarityName) ?? s.RarityName).ToUpperInvariant(),
                    PrintVariant: s.PrintVariant))
                .ToDictionary(
                    g => g.Key,
                    g => (IReadOnlySet<CardEdition>)g
                        .Select(s => CardEditionExtensions.TryParseTCGAPIEditionName(s.Edition, out var e) ? (CardEdition?)e : null)
                        .Where(e => e.HasValue)
                        .Select(e => e!.Value)
                        .ToHashSet());

            return Task.FromResult<IReadOnlyDictionary<(string, string, string?), IReadOnlySet<CardEdition>>>(map);
        }

        public Task<decimal?> GetPrintingPriceAsync(int cardID, string setCode, string rarityName, CardEdition? edition = null, string? printVariant = null)
        {
            var cardSets = _pricingDataCache.GetCardSets(cardID);
            var match = FindMatch(cardSets, setCode, rarityName, edition, printVariant);

            return Task.FromResult(match is null || match.Price == 0m ? (decimal?)null : match.Price);
        }

        private static TCGPriceSet? FindMatch(IEnumerable<TCGPriceSet> cardSets, string setCode, string rarityName, CardEdition? edition, string? printVariant)
        {
            bool IsSetRarityVariantMatch(TCGPriceSet s) =>
                string.Equals(s.Code, setCode, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(RarityExtensions.NormalizeRarityName(s.RarityName), RarityExtensions.NormalizeRarityName(rarityName), StringComparison.OrdinalIgnoreCase) &&
                string.Equals(s.PrintVariant, printVariant, StringComparison.OrdinalIgnoreCase);

            if (edition is not null)
            {
                var editionMatch = cardSets.FirstOrDefault(s =>
                    IsSetRarityVariantMatch(s) &&
                    string.Equals(s.Edition, edition.Value.GetTCGAPIEditionName(), StringComparison.OrdinalIgnoreCase));

                if (editionMatch is not null)
                    return editionMatch;
            }

            // No exact edition match — fall back to set/rarity/variant only (older sets may not carry set_edition).
            return cardSets.FirstOrDefault(IsSetRarityVariantMatch);
        }
    }
}
