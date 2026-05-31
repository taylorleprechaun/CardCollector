using CardCollector.DTO;
using Newtonsoft.Json;

namespace CardCollector.Services
{
    public sealed class PricingService : IPricingService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<PricingService> _logger;

        public PricingService(IHttpClientFactory httpClientFactory, ILogger<PricingService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<decimal?> GetPrintingPriceAsync(int cardID, string setCode, string rarityName)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("YGOProDeck");
                var json = await client.GetStringAsync($"api/v7/cardinfo.php?id={cardID}&tcgplayer_data=true").ConfigureAwait(false);

                var result = JsonConvert.DeserializeObject<TCGPriceCardArray>(json);
                var cardSets = result?.Cards?.FirstOrDefault()?.CardSets;
                if (cardSets is null)
                    return null;

                var match = cardSets.FirstOrDefault(s =>
                    string.Equals(s.Code, setCode, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(s.RarityName, rarityName, StringComparison.OrdinalIgnoreCase));

                if (match is null || match.Price == 0m)
                    return null;

                return match.Price;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch price for card {CardID} set {SetCode} rarity {Rarity}", cardID, setCode, rarityName);
                return null;
            }
        }
    }
}
