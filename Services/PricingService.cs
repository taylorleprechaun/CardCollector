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

        public Task<IReadOnlyDictionary<int, IReadOnlySet<CardEdition>>> GetEditionsByPrintingAsync(int cardID)
        {
            var map = _pricingDataCache.GetPrintingRowsByCard(cardID).ToDictionary(
                kvp => kvp.Key,
                kvp => (IReadOnlySet<CardEdition>)kvp.Value
                    .Select(s => CardEditionExtensions.TryParseTCGAPIEditionName(s.Edition, out var e) ? (CardEdition?)e : null)
                    .Where(e => e.HasValue)
                    .Select(e => e!.Value)
                    .ToHashSet());
            return Task.FromResult<IReadOnlyDictionary<int, IReadOnlySet<CardEdition>>>(map);
        }

        public Task<decimal?> GetPrintingPriceAsync(int printingID, CardEdition? edition = null)
        {
            var rows = _pricingDataCache.GetPrintingRows(printingID);
            var match = (edition is null
                    ? null
                    : rows.FirstOrDefault(s => string.Equals(s.Edition, edition.Value.GetTCGAPIEditionName(), StringComparison.OrdinalIgnoreCase)))
                ?? rows.FirstOrDefault();
            return Task.FromResult(match is null || match.Price == 0m ? (decimal?)null : match.Price);
        }
    }
}
