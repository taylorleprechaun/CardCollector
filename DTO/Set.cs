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

        /// <summary>The catalog printing this row is; 0 for rows that don't come from the catalog.</summary>
        [JsonIgnore]
        public int PrintingID { get; set; }

        /// <summary>
        /// Null for a normal/base print; otherwise TCGPlayer's label for a distinct product of the same rarity (e.g.
        /// "Extended Art"), since yaml-yugi and YGOProDeck track rarity but not sellable print variants.
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

        /// <summary>The TCGPlayer products linked to this printing; prices come from these.</summary>
        [JsonIgnore]
        public IReadOnlyList<int> TCGProductIDs { get; set; } = [];
    }
}
