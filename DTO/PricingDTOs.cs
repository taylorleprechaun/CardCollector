using Newtonsoft.Json;

namespace CardCollector.DTO
{
    public class TCGPriceSet
    {
        [JsonProperty("set_code")]
        public string Code { get; set; } = string.Empty;

        [JsonProperty("set_edition")]
        public string Edition { get; set; } = string.Empty;

        public decimal Price => decimal.TryParse(PriceRaw, out var p) ? p : 0m;

        [JsonProperty("set_price")]
        public string PriceRaw { get; set; } = "0";

        [JsonProperty("set_rarity")]
        public string RarityName { get; set; } = string.Empty;
    }

    public class TCGPriceCard
    {
        [JsonProperty("card_sets")]
        public IEnumerable<TCGPriceSet> CardSets { get; set; } = [];

        [JsonProperty("id")]
        public int ID { get; set; }
    }

    internal class TCGPriceCardArray
    {
        [JsonProperty("data")]
        public IEnumerable<TCGPriceCard> Cards { get; set; } = [];

        [JsonProperty("meta")]
        public TCGPriceMeta? Meta { get; set; }
    }

    internal class TCGPriceMeta
    {
        [JsonProperty("rows_remaining")]
        public int RowsRemaining { get; set; }
    }
}
