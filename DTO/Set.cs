using Newtonsoft.Json;

namespace CardCollector.DTO
{
    public class Set
    {
        [JsonProperty("set_code")]
        public string? Code { get; set; }

        [JsonProperty("set_name")]
        public string? Name { get; set; }

        [JsonProperty("set_price")]
        public decimal Price { get; set; }

        /// <summary>
        /// Null for a normal/base print. Populated by <see cref="Repository.CardDataMapper.EnrichWithPrintVariants"/>
        /// from the tcgcsv catalog, since yaml-yugi and YGOProDeck's own set data track rarity but not distinct
        /// sellable print variants (e.g. "Extended Art") of the same rarity.
        /// </summary>
        [JsonProperty("print_variant")]
        public string? PrintVariant { get; set; }

        // Parsed enum for filtering/comparison. JsonIgnore because RarityName owns the JSON key.
        [JsonIgnore]
        public Rarity Rarity => RarityExtensions.ParseRarity(RarityName);

        [JsonProperty("set_rarity_code")]
        public string? RarityCode { get; set; }

        // Raw string from JSON — used for display and as the source for Rarity parsing.
        [JsonProperty("set_rarity")]
        public string? RarityName { get; set; }
    }
}
