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

    public class TCGCatalogEnvelope<T>
    {
        [JsonProperty("errors")]
        public IEnumerable<string> Errors { get; set; } = [];

        [JsonProperty("results")]
        public IEnumerable<T> Results { get; set; } = [];

        [JsonProperty("success")]
        public bool Success { get; set; }
    }

    public class TCGCatalogGroup
    {
        [JsonProperty("abbreviation")]
        public string? Abbreviation { get; set; }

        [JsonProperty("groupId")]
        public int GroupID { get; set; }
    }

    public class TCGCatalogPrice
    {
        [JsonProperty("marketPrice")]
        public decimal? MarketPrice { get; set; }

        [JsonProperty("productId")]
        public int ProductID { get; set; }

        [JsonProperty("subTypeName")]
        public string? SubTypeName { get; set; }
    }

    public class TCGCatalogProduct
    {
        [JsonProperty("extendedData")]
        public IEnumerable<TCGCatalogProductField> ExtendedData { get; set; } = [];

        [JsonProperty("productId")]
        public int ProductID { get; set; }
    }

    public class TCGCatalogProductField
    {
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("value")]
        public string? Value { get; set; }
    }

}
