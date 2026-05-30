using Newtonsoft.Json;

namespace CardCollector.DTO
{
    internal class TcgPriceCardArray
    {
        [JsonProperty("data")]
        public IEnumerable<TcgPriceCard> Cards { get; set; } = [];
    }

    internal class TcgPriceCard
    {
        [JsonProperty("card_sets")]
        public IEnumerable<TcgPriceSet> CardSets { get; set; } = [];
    }

    internal class TcgPriceSet
    {
        [JsonProperty("set_code")]
        public string Code { get; set; } = string.Empty;

        [JsonProperty("set_rarity")]
        public string RarityName { get; set; } = string.Empty;

        [JsonProperty("set_price")]
        public string PriceRaw { get; set; } = "0";

        public decimal Price => decimal.TryParse(PriceRaw, out var p) ? p : 0m;
    }
}
