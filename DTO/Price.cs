using Newtonsoft.Json;

namespace CardCollector.DTO
{
    public class Price
    {
        [JsonProperty("amazon_price")]
        public decimal Amazon { get; set; }

        [JsonProperty("cardmarket_price")]
        public decimal CardMarket { get; set; }

        [JsonProperty("coolstuffinc_price")]
        public decimal CoolStuffInc { get; set; }

        [JsonProperty("ebay_price")]
        public decimal Ebay { get; set; }

        [JsonProperty("tcgplayer_price")]
        public decimal TCGPlayer { get; set; }
    }
}
