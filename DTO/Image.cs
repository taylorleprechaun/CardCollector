using Newtonsoft.Json;

namespace CardCollector.DTO
{
    public class Image
    {
        [JsonProperty("id")]
        public int ID { get; set; }

        [JsonProperty("image_url")]
        public string? ImageURL { get; set; }

        [JsonProperty("image_url_small")]
        public string? ImageURLSmall { get; set; }
    }
}
