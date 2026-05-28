using Newtonsoft.Json;

namespace CardCollector.DTO
{
    public class CardArray
    {
        [JsonProperty("data")]
        public IEnumerable<Card> Cards { get; set; }
    }
}
