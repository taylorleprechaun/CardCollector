using Newtonsoft.Json;

namespace CardCollector.DTO
{
    public class Card
    {
        [JsonProperty("archetype")]
        public string? Archetype { get; set; }

        [JsonProperty("atk")]
        public int? ATK { get; set; }

        [JsonProperty("attribute")]
        public string? Attribute { get; set; }

        [JsonProperty("card_images")]
        public IEnumerable<Image>? CardImages { get; set; }

        [JsonProperty("card_prices")]
        public IEnumerable<Price>? CardPrices { get; set; }

        [JsonProperty("card_sets")]
        public IEnumerable<Set>? CardSets { get; set; }

        [JsonProperty("type")]
        public string? CardType { get; set; }

        [JsonProperty("def")]
        public int? DEF { get; set; }

        [JsonProperty("desc")]
        public string? Description { get; set; }

        [JsonProperty("id")]
        public int ID { get; set; }

        [JsonProperty("level")]
        public int? Level { get; set; }

        [JsonProperty("linkmarkers")]
        public IEnumerable<string>? LinkMarkers { get; set; }

        [JsonProperty("linkval")]
        public int? LinkRating { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("race")]
        public string? Type { get; set; }

        [JsonProperty("typeline")]
        public IEnumerable<string>? TypeLine { get; set; }
    }
}
