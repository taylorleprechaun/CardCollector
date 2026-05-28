using Newtonsoft.Json;

namespace CardCollector.DTO
{
    public class Set
    {
        [JsonProperty("set_code")]
        public string Code { get; set; }

        [JsonProperty("set_name")]
        public string Name { get; set; }

        [JsonProperty("set_price")]
        public decimal Price { get; set; }

        // Parsed enum for filtering/comparison. JsonIgnore because RarityName owns the JSON key.
        [JsonIgnore]
        public Rarity Rarity => RarityExtensions.ParseRarity(RarityName);

        // Raw string from JSON — used for display and as the source for Rarity parsing.
        [JsonProperty("set_rarity")]
        public string RarityName { get; set; }

        [JsonProperty("set_rarity_code")]
        public string RarityCode { get; set; }
    }
}
