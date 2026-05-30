using Newtonsoft.Json;

namespace CardCollector.DTO
{
    public class CardSetData
    {
        [JsonProperty("set_name")]     public string Name { get; set; } = string.Empty;
        [JsonProperty("set_code")]     public string Code { get; set; } = string.Empty;
        [JsonProperty("num_of_cards")] public int NumOfCards { get; set; }
        [JsonProperty("tcg_date")]     public string? TCGDate { get; set; }
        [JsonProperty("set_image")]    public string? SetImage { get; set; }
    }
}
